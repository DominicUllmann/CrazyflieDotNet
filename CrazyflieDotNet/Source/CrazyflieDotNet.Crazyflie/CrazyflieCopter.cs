using CrazyflieDotNet.Crazyflie.Feature;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.Crazyradio.Driver;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CrazyflieDotNet.Crazyflie
{
    public class CrazyflieCopter
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(CrazyflieCopter));

        private CrtpCommunicator _communicator;
        private ICrazyradioDriver _crazyradioDriver;

        private Commander _commander;
        private HighlevelCommander _highlevelCommander;
        private Logger _logger;
        private ParamConfigurator _paramConfigurator;
        private PlatformService _platformService;
        private DirectoryInfo _cacheDirectory;

        /// <summary>
        /// Creates a new instance to communicate with one crazycopter.
        /// </summary>
        /// <param name="cacheDirectory">set cacheDirectory to the directory where to store table of contents downloaded from the crazyfly for
        /// connection speed up. Default: .\cache</param>
        public CrazyflieCopter(DirectoryInfo cacheDirectory = null)
        {            
            if (cacheDirectory == null)
            {
                cacheDirectory = new DirectoryInfo(@".\cache");
            }
            _cacheDirectory = cacheDirectory;
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

        public ICrazyflieHighlevelCommander HighLevelCommander
        {
            get
            {
                if (_highlevelCommander == null)
                {
                    throw new InvalidOperationException("call connect first");
                }
                return _highlevelCommander;
            }
        }

        private Task StartConnection(RadioChannel? channelToFilterFor = null, RadioDataRate? dataRateToFilterfor = null)
        {
            return Task.Run(() =>
            {
                _crazyradioDriver = SetupCrazyflieDriver();
                ConnectToCrazyflie(channelToFilterFor, dataRateToFilterfor);
                _communicator = new CrtpCommunicator(_crazyradioDriver);
                _communicator.Start();
            });
        }

        /// <summary>
        /// Connect to the first or matching copter.
        /// </summary>
        /// <param name="channelToFilterFor">The channel the copter is using.</param>
        /// <param name="dataRateToFilterfor">The data rate it communicates with.</param>
        public async Task Connect(RadioChannel? channelToFilterFor = null, RadioDataRate? dataRateToFilterfor = null)
        {
            await StartConnection(channelToFilterFor, dataRateToFilterfor);

            _platformService = new PlatformService(_communicator);
            await _platformService.FetchPlatformInformations();                
                       
            bool useV2 = _platformService.ProtocolVersion >= 4;

            _paramConfigurator = new ParamConfigurator(_communicator, useV2, _cacheDirectory);
            _logger = new Logger(_communicator, useV2, _cacheDirectory);

            await _paramConfigurator.RefreshToc();
            await _logger.RefreshToc();

            _commander = new Commander(_communicator, false);
            _highlevelCommander = new HighlevelCommander(_communicator, _paramConfigurator);
        }

        /// <summary>
        /// Stop communiation with the copter and shutdown the radio driver.
        /// </summary>
        public async void Disconnect()
        {
            try
            {
                try
                {
                    if (_paramConfigurator != null)
                    {
                        await Task.Run(() => _paramConfigurator.Stop());
                    }
                }
                finally
                {
                    if (_communicator != null)
                    {
                        await Task.Run(() => _communicator.Stop());
                    }
                }
            }
            finally
            {
                if (_crazyradioDriver != null)
                {
                    await Task.Run(() => _crazyradioDriver.Close());
                }
            }
        }

        private void ConnectToCrazyflie(RadioChannel? channelToFilterFor, RadioDataRate? dataRateToFilterfor)
        {
            try
            {
                var scanResults = _crazyradioDriver.ScanChannels();
                // scanresults collection contains a list per dataRate

                if (dataRateToFilterfor != null)
                {
                    scanResults = scanResults.Where(x => x.DataRate == dataRateToFilterfor.Value);
                }

                RadioChannel? selectedChannel = null;
                ScanChannelsResult selectedRate = null;
                if (scanResults.Any()) 
                {                    
                    selectedRate = scanResults.First(); // take the first or first matching.
                    
                    // Use first or matching online Crazyflie quadcopter found
                    if (channelToFilterFor != null)
                    {
                        selectedChannel = selectedRate.Channels.FirstOrDefault(x => x == channelToFilterFor.Value);
                    }
                    else
                    {
                        selectedChannel = selectedRate.Channels.First();
                    }                                                            
                }
                if (selectedRate != null && selectedChannel != null)
                {
                    // Set CrazyradioDriver's DataRate and Channel to that of online Crazyflie
                    _crazyradioDriver.DataRate = selectedRate.DataRate;
                    _crazyradioDriver.Channel = selectedChannel;
                    _log.Info($"found Crazyflie quadcopter with {selectedRate.DataRate} / {selectedChannel} ");
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
