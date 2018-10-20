using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrazyflieDotNet.Crazyflie.Feature.Log;
using log4net;

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
        private static readonly ILog _log = LogManager.GetLogger(typeof(Navigator));

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

        public async Task Stop()
        {
            if (_isRunning)
            {
                _copter.Logger.StopConfig(_navLogConfig);
                _copter.Logger.DeleteConfig(_navLogConfig);
            }
            _isRunning = false;
            _navLogConfig = null;
            _copter.HighLevelCommander.Stop();
            await _copter.HighLevelCommander.Disable();
        }

        public async Task Takeoff(float height, float velocity = 0.2f)
        {
            if (_isFlying)
            {
                throw new InvalidOperationException("already flying");
            }
            var duration_s = height / velocity;

            await _copter.HighLevelCommander.Enable();

            await _copter.ParamConfigurator.SetValue("kalman.resetEstimation", (byte)1);
            await Task.Delay(100);

            await  _copter.ParamConfigurator.SetValue("kalman.resetEstimation", (byte)0);
            await Task.Delay(1000);

            _copter.HighLevelCommander.Takeoff(0.5f, duration_s);
            _isFlying = true;
            
            await Task.Delay(TimeSpan.FromSeconds(duration_s));
            _log.Info("Takeoff complete");
        }

        public async Task Land(float height = 0, float velocity = 0.2f)
        {
            if (!_isFlying)
            {
                throw new InvalidOperationException("start first");                
            }
            var positionn = CurrentPosition;

            var duration_s = (positionn.Z - height) / velocity;
            _log.Info($"Landing from {positionn.X} {positionn.Y} {positionn.Z} in {duration_s}");

            _copter.HighLevelCommander.Land(height, duration_s);

            // add 0.2 second in addition to ensure it had time to settle down.
            await Task.Delay(TimeSpan.FromSeconds(duration_s + 0.2)); 
            _log.Info("Land complete");            
        }

        private float CalcuatleDistanceToPosition(float x, float y, float z)
        {
            var position = CurrentPosition;
            var dx = x - position.X;
            var dy = y - position.Y;
            var dz = z - position.Z;
            var distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            return distance;
        }

        private float CalculateDurationToPositon(float x, float y, float z, float velocity)
        {
            var distance = CalcuatleDistanceToPosition(x, y, z);

            var duration_s = distance / velocity;
            return duration_s;
        }

        public async Task NavigateTo(float x, float y, float z, float velocity = 0.2f, float variance = 0.05f)
        {
            _log.Info($"fly to {x} {y} {z}");
            var duration_s = CalculateDurationToPositon(x, y, z, velocity);
            _copter.HighLevelCommander.GoTo(x, y, z, 0f, duration_s);
            await Task.Delay(TimeSpan.FromSeconds(duration_s));
            
            var position = CurrentPosition;
            if (!(Math.Abs(position.X - x) < variance &&
                  Math.Abs(position.Y - y) < variance &&
                  Math.Abs(position.Z - z) < variance))
            {
                duration_s = Math.Min(0.1f, CalculateDurationToPositon(x, y, z, velocity));
                _copter.HighLevelCommander.GoTo(x, y, z, 0f, duration_s);
                await Task.Delay(TimeSpan.FromSeconds(duration_s));                
            }
            var distance = CalcuatleDistanceToPosition(x, y, z);
            _log.Info($"navigation to {x} {y} {z} complete. Distance {distance}");
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

        public async Task WaitForCalibratedPosition(TimeSpan timeout)
        {
            var cancelToken = new CancellationTokenSource();
            var timeoutTask = Task.Delay(timeout);
            var calibrationTask = WaitForCalibratedPosition(cancelToken.Token);
            await Task.WhenAny(calibrationTask, timeoutTask);

            if (!calibrationTask.IsCompleted)
            {
                cancelToken.Cancel();
            }

            throw new ApplicationException("calibration failed");
        }

        public async Task WaitForCalibratedPosition(CancellationToken cancellationToken)
        {
            var threshold = 0.001f;
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Run(() =>
                {
                    // wait at most for next update; afterwards check if timeout has been reached.
                    _waitForVarianceUpdate.WaitOne(_updatePeriodInMs + 20);
                });
                if (!_firstValueTracked)
                {
                    continue;
                }

                if (VarianceX < threshold && VarianceY < threshold && VarianceZ < threshold)
                {
                    return;
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
