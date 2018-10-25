using CrazyflieDotNet.Crazyflie.Feature;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.Crazyradio;
using log4net;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CrazyflieDotNet.Crazyflie
{
    public class CrazyflieCopter
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(CrazyflieCopter));

        private ICrazyradioManager _radioManager;
        private CrtpCommunicator _communicator;
        private CrazyflieUri _uri;

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
        public CrazyflieCopter(ICrazyradioManager radioManager, DirectoryInfo cacheDirectory = null)
        {            
            if (cacheDirectory == null)
            {
                cacheDirectory = new DirectoryInfo(@".\cache");
            }
            _cacheDirectory = cacheDirectory;
            _radioManager = radioManager;
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

        private Task StartConnection(CrazyflieUri uri)
        {
            _uri = uri;
            return Task.Run(() =>
            {
                _communicator = new CrtpCommunicator(_radioManager.SelectRadio(uri));
                _communicator.Start();
            });
        }

        /// <summary>
        /// Connect to the specified copter.
        /// </summary>
        public async Task Connect(CrazyflieUri uri)
        {
            await StartConnection(uri);

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
                if (_radioManager != null)
                {
                    await Task.Run(() => _radioManager.DeselectRadio(_uri));
                }
            }
        }


    }
}
