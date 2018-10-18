using System;
using System.Collections.Generic;
using System.Threading;
using CrazyflieDotNet.Crazyflie.Feature.Log;

namespace CrazyflieDotNet.Crazyflie.Feature.Localization
{

    public class PositionUpdateEventArgs {

        public Position CurrentPosition { get; }
        
        public PositionUpdateEventArgs(Position position)
        {
            CurrentPosition = position;
        }
    }

    public delegate void PositionUpdateEventHandler(object sender, PositionUpdateEventArgs args);

    /// <summary>
    /// Higher level abstraction for position tracking.
    /// Required kalman to be present in the crazyfly.
    /// </summary>
    public class Navigator
    {
        private CrazyflieCopter _copter;

        private readonly List<float> _historyVarianceX = new List<float>();
        private readonly List<float> _historyVarianceY = new List<float>();
        private readonly List<float> _historyVarianceZ = new List<float>();

        public float VarianceX { get; private set; }
        public float VarianceY { get; private set; }
        public float VarianceZ { get; private set; }

        private ManualResetEvent _waitForVarianceUpdate = new ManualResetEvent(false);
        private ushort _updatePeriodInMs;

        private bool _isRunning;
        private bool _isFlying;
        private bool _firstValueTracked;
        private LogConfig _navLogConfig;


        public Navigator(CrazyflieCopter copter)
        {
            _copter = copter;
        }

        public Position CurrentPosition
        {
            get;
            private set;
        }

        /// <summary>
        /// Event for receiving position updates.
        /// </summary>
        public event PositionUpdateEventHandler PositionUpdate;

        public void Start(ushort updatePeriodInMs)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("already running");
            }
            if (!_copter.Logger.IsLogVariableKnown("kalman.stateX"))
            {
                throw new InvalidOperationException("require kalman to be present in the firmware of the crazyflie");
            }
            _updatePeriodInMs = updatePeriodInMs;
            _firstValueTracked = false;
            InitVarianceHistory();

            _navLogConfig = _copter.Logger.CreateEmptyLogConfigEntry("Kalman Variance", updatePeriodInMs);
            // current estimated position
            _navLogConfig.AddVariable("kalman.stateX", "float");
            _navLogConfig.AddVariable("kalman.stateY", "float");
            _navLogConfig.AddVariable("kalman.stateZ", "float");
            // position variance
            _navLogConfig.AddVariable("kalman.varPX", "float");
            _navLogConfig.AddVariable("kalman.varPY", "float");
            _navLogConfig.AddVariable("kalman.varPZ", "float");
            _navLogConfig.LogDataReceived += Config_LogKalmanDataReceived;
            _copter.Logger.AddConfig(_navLogConfig);
            _copter.Logger.StartConfig(_navLogConfig);

            _isRunning = true;
        }

        public void Stop()
        {
            if (_isRunning)
            {
                _copter.Logger.StopConfig(_navLogConfig);
                _copter.Logger.DeleteConfig(_navLogConfig);
            }
            _isRunning = false;
            _navLogConfig = null;
        }

        public void Takeoff(float height, float velocity = 0.2f)
        {
            if (_isFlying)
            {
                throw new InvalidOperationException("already flying");
            }
            _copter.HighLevelCommander.Enable().Wait();
            _copter.ParamConfigurator.SetValue("kalman.resetEstimation", (byte)1).Wait();
            Thread.Sleep(100);
            _copter.ParamConfigurator.SetValue("kalman.resetEstimation", (byte)0).Wait();
            Thread.Sleep(1000);

            _isFlying = true;

            var duration_s = height / velocity;
            _copter.HighLevelCommander.Takeoff(0.5f, duration_s);
            Thread.Sleep(TimeSpan.FromSeconds(duration_s));
        }

        public void Land(float height = 0, float velocity = 0.2f)
        {
            if (!_isFlying)
            {
                throw new InvalidOperationException("start first");                
            }

            var duration_s = (CurrentPosition.Z - height) / velocity;
            _copter.HighLevelCommander.Land(height, duration_s);
            Thread.Sleep(TimeSpan.FromSeconds(duration_s));

            _copter.HighLevelCommander.Disable().Wait();
        }

        private float CalculateDurationToPositon(float x, float y, float z, float velocity)
        {
            var dx = x - CurrentPosition.X;
            var dy = y - CurrentPosition.Y;
            var dz = z - CurrentPosition.Z;
            var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            var duration_s = (float)distance / velocity;
            return duration_s;
        }

        public void NavigateTo(float x, float y, float z, float velocity = 0.2f, float variance = 0.05f)
        {
            var duration_s = CalculateDurationToPositon(x, y, z, velocity);
            _copter.HighLevelCommander.GoTo(x, y, z, 0f, duration_s);
            Thread.Sleep(TimeSpan.FromSeconds(duration_s));

            var i = 0;
            while (!(Math.Abs(CurrentPosition.X - x) < variance &&
                Math.Abs(CurrentPosition.Y - y) < variance &&
                Math.Abs(CurrentPosition.Z - z) < variance) && i < 4)
            {
                duration_s = CalculateDurationToPositon(x, y, z, velocity);
                Thread.Sleep(TimeSpan.FromSeconds(duration_s));

                _copter.HighLevelCommander.GoTo(x, y, z, 0f, duration_s);
                i++;
            }
        }

        /// <summary>
        /// This navigates to the position specified and returns as soon as
        /// it has reached that position (as observed by the navigation system).
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public bool NavigateTo(float x, float y, float z, TimeSpan timeout, float variance)
        {
            DateTime startTime = DateTime.Now;
            while (true)
            {
                _copter.Commander.SendPositionSetpoint(x, y, z, 0);
                if (startTime + timeout < DateTime.Now)
                {
                    return false; 
                }
                if (Math.Abs(CurrentPosition.X - x) < variance &&
                    Math.Abs(CurrentPosition.Y - y) < variance &&
                    Math.Abs(CurrentPosition.Z - z) < variance)
                {
                    break;
                }
                // ensure that we send a command at least every 250 ms and at most every 100ms
                // optimally, we wait for 3 updates of position until we send again.
                Thread.Sleep(Math.Max(100, Math.Min(400, 3 * _updatePeriodInMs)));
            }
            return true;
        }

        private void InitVarianceHistory()
        {
            InitVarianceHistory(_historyVarianceX);
            InitVarianceHistory(_historyVarianceY);
            InitVarianceHistory(_historyVarianceZ);
        }

        private void InitVarianceHistory(List<float> history)
        {
            history.Clear();
            for (int i = 0; i < 10; i++)
            {
                history.Add(1000);
            }
        }

        public void WaitForCalibratedPosition(TimeSpan timeout)
        {
            var startTime = DateTime.Now;
            var threshold = 0.001f;
            while (true)
            {
                // wait at most for next update; afterwards check if timeout has been reached.
                _waitForVarianceUpdate.WaitOne(_updatePeriodInMs + 20);
                if (!_firstValueTracked)
                {
                    continue;
                }

                if (VarianceX < threshold && VarianceY < threshold && VarianceZ < threshold)
                {
                    return;
                }
                if (DateTime.Now > startTime + timeout)
                {
                    throw new ApplicationException("calibration failed.");
                }
                _waitForVarianceUpdate.Reset();                
            }            
        }
         

        private void Config_LogKalmanDataReceived(object sender, LogDataReceivedEventArgs e)
        {
            _firstValueTracked = true;

            _historyVarianceX.Add((float)e.GetVariable("kalman.varPX"));
            _historyVarianceY.Add((float)e.GetVariable("kalman.varPY"));
            _historyVarianceZ.Add((float)e.GetVariable("kalman.varPZ"));

            _historyVarianceX.RemoveAt(0);
            _historyVarianceY.RemoveAt(0);
            _historyVarianceZ.RemoveAt(0);

            VarianceX = CalculatueMax(_historyVarianceX) - CalculatueMin(_historyVarianceX);
            VarianceY = CalculatueMax(_historyVarianceY) - CalculatueMin(_historyVarianceY);
            VarianceZ = CalculatueMax(_historyVarianceZ) - CalculatueMin(_historyVarianceZ);

            CurrentPosition = new Position((float)e.GetVariable("kalman.stateX"), (float)e.GetVariable("kalman.stateY"), (float)e.GetVariable("kalman.stateZ"));
            PositionUpdate?.Invoke(this, new PositionUpdateEventArgs(CurrentPosition));
            _waitForVarianceUpdate.Set();
        }

        private float CalculatueMin(List<float> historyVariance)
        {
            var result = 1000f;
            foreach (var x in historyVariance)
            {
                result = Math.Min(result, x);
            }
            return result;
        }

        private float CalculatueMax(List<float> historyVariance)
        {
            var result = 0f;
            foreach (var x in historyVariance)
            {
                result = Math.Max(result, x);
            }
            return result;
        }
    }
}
