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
        private ICrazyflieLogger _logger;

        private readonly List<float> _historyVarianceX = new List<float>();
        private readonly List<float> _historyVarianceY = new List<float>();
        private readonly List<float> _historyVarianceZ = new List<float>();

        public float VarianceX { get; private set; }
        public float VarianceY { get; private set; }
        public float VarianceZ { get; private set; }

        private ManualResetEventSlim _waitForVarianceUpdate = new ManualResetEventSlim(false);
        private ushort _updatePeriodInMs;

        private bool _isRunning;
        private bool _firstValueTracked;
        private LogConfig _navLogConfig;


        public Navigator(ICrazyflieLogger logger)
        {
            _logger = logger;
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

        public void StartTracking(ushort updatePeriodInMs)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("already running");
            }
            if (!_logger.IsLogVariableKnown("kalman.stateX"))
            {
                throw new InvalidOperationException("require kalman to be present in the firmware of the crazyflie");
            }
            _updatePeriodInMs = updatePeriodInMs;
            _firstValueTracked = false;
            InitVarianceHistory();

            _navLogConfig = _logger.CreateEmptyLogConfigEntry("Kalman Variance", updatePeriodInMs);
            // current estimated position
            _navLogConfig.AddVariable("kalman.stateX", "float");
            _navLogConfig.AddVariable("kalman.stateY", "float");
            _navLogConfig.AddVariable("kalman.stateZ", "float");
            // position variance
            _navLogConfig.AddVariable("kalman.varPX", "float");
            _navLogConfig.AddVariable("kalman.varPY", "float");
            _navLogConfig.AddVariable("kalman.varPZ", "float");
            _navLogConfig.LogDataReceived += Config_LogKalmanDataReceived;            
            _logger.AddConfig(_navLogConfig);
            _logger.StartConfig(_navLogConfig);

            _isRunning = true;
        }

        public void StopTracking()
        {
            if (_isRunning)
            {
                _logger.StopConfig(_navLogConfig);
                _logger.DeleteConfig(_navLogConfig);
            }
            _isRunning = false;
            _navLogConfig = null;
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
                _waitForVarianceUpdate.Wait(_updatePeriodInMs + 20);
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
