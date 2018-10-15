using CrazyflieDotNet.Crazyflie.Feature;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.Crazyradio.Driver;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyflieDotNet.Crazyflie
{
    public class CrazyflieCopter
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(CrazyflieCopter));

        private CrtpCommunicator _communicator;
        private ICrazyradioDriver _crazyradioDriver;

        private Commander _commander;
        private Logger _logger;
        private ParamConfigurator _paramConfigurator;
        private PlatformService _platformService;

        public CrazyflieCopter()
        {            
        }

        /// <summary>
        /// Gets the logger after crazyflie connected.
        /// </summary>
        public ICrazyflieLogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    throw new InvalidOperationException("call connect first");
                }
                return _logger;
            }
        }

        public ICrazyflieCommander Commander
        {
            get
            {
                if (_commander == null)
                {
                    throw new InvalidOperationException("call connect first");
                }
                return _commander;
            }
        }

        public ICrazyflieParamConfigurator ParamConfigurator
        {
            get
            {
                if (_paramConfigurator == null)
                {
                    throw new InvalidOperationException("call connect first");
                }
                return _paramConfigurator;
            }
        }

        public void Connect()
        {
            _crazyradioDriver = SetupCrazyflieDriver();
            ConnectToCrazyflie();
            _communicator = new CrtpCommunicator(_crazyradioDriver);
            _communicator.Start();
            _platformService = new PlatformService(_communicator);
            _platformService.FetchPlatformInformations().Wait();
            bool useV2 = _platformService.ProtocolVersion >= 4;
            _paramConfigurator = new ParamConfigurator(_communicator, useV2);
            _logger = new Logger(_communicator, useV2);
            var paramTask =_paramConfigurator.RefreshToc();
            var logTaks = _logger.RefreshToc();
            paramTask.Wait();
            logTaks.Wait();
            _commander = new Commander(_communicator, false);
        }

        public void Disconnect()
        {
            try
            {
                if (_communicator != null)
                {
                    _communicator.Stop();
                }
            }
            finally
            {
                if (_crazyradioDriver != null)
                {
                    _crazyradioDriver.Close();
                }
            }
        }

        private void ConnectToCrazyflie()
        {
            try
            {
                var scanResults = _crazyradioDriver.ScanChannels();
                if (scanResults.Any())
                {
                    // Use first online Crazyflie quadcopter found
                    var firstScanResult = scanResults.First();

                    // Set CrazyradioDriver's DataRate and Channel to that of online Crazyflie
                    var dataRateWithCrazyflie = firstScanResult.DataRate;
                    var channelWithCrazyflie = firstScanResult.Channels.First();
                    _crazyradioDriver.DataRate = dataRateWithCrazyflie;
                    _crazyradioDriver.Channel = channelWithCrazyflie;
                }
                else
                {
                    _log.Error("No Crazyflie quadcopters available for communication.");
                    throw new ApplicationException("Crazyflie not found");
                }
            }
            catch (Exception ex)
            {
                _log.Error("fail to connect to crarzyflie.", ex);
                throw new ApplicationException("Can't connect to crazyflie");

            }
        }

        private ICrazyradioDriver SetupCrazyflieDriver()
        {
            IEnumerable<ICrazyradioDriver> crazyradioDrivers = null;

            try
            {
                // Scan for connected Crazyradio USB dongles
                crazyradioDrivers = CrazyradioDriver.GetCrazyradios();
            }
            catch (Exception ex)
            {
                var msg = "Error getting Crazyradio USB dongle devices connected to computer.";
                _log.Error(msg, ex);
                throw new ApplicationException(msg, ex);
            }

            // If we found any
            if (crazyradioDrivers != null && crazyradioDrivers.Any())
            {
                try
                {
                    // Use first available Crazyradio dongle
                    var crazyradioDriver = crazyradioDrivers.First();
                    crazyradioDriver.Open();
                    return crazyradioDriver;
                }
                catch (Exception ex)
                {
                    _log.Error("Failed to enable crazy radio", ex);
                    throw new ApplicationException("Failed to enable crazy radio");
                }
            }
            else
            {
                _log.Error("no crarzyradio USB dongle device found");
                throw new ApplicationException("no crazyradio found. check if usb device connected and libusb driver installed");
            }            
        }


    }
}
