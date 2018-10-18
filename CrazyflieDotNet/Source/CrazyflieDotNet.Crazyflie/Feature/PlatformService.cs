using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using log4net;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrazyflieDotNet.Crazyflie.Feature
{

    /// <summary>
    /// Used for retrieving / setting platform settings.
    /// </summary>
    public class PlatformService
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(PlatformService));

        private const byte LINKSERVICE_SOURCE_CHANNEL = 1;
        private const byte VERSION_COMMAND_CHANNEL = 1;
        private const byte PLATFORM_COMMAND_CHANNEL = 0;

        private const byte VERSION_GET_PROTOCOL_COMMAND = 0;

        private ICrtpCommunicator _communicator;
        private ManualResetEvent _waitForProtocolResult = new ManualResetEvent(false);

        private static Encoding _encoder = Encoding.UTF8;

        public int ProtocolVersion { get; private set; } = -1;

        public PlatformService(ICrtpCommunicator communicator)
        {
            _communicator = communicator;
            _communicator.RegisterEventHandler((byte)CrtpPort.PLATFORM,
                PlatformMessageReceived);
            _communicator.RegisterEventHandler((byte)CrtpPort.LINKCTRL,
                CrtServiceMessageReceived);
        }

        /// <summary>
        /// Enable/disable the client side X-mode.When enabled this recalculates
        /// the setpoints before sending them to the Crazyflie.
        /// </summary>        
        public void SetContinousWave(bool enabled)
        {
            var msg = new CrtpMessage((byte)CrtpPort.PLATFORM, PLATFORM_COMMAND_CHANNEL,
                new byte[] { 0, Convert.ToByte(enabled) });
            _communicator.SendMessage(msg);
        }

        /// <summary>
        /// Fetch platform info from the firmware
        /// Should be called at the earliest in the connection sequence
        /// </summary>
        public Task<int> FetchPlatformInformations()
        {
            ProtocolVersion = -1;
            return Task.Run(() => RequestProtocolVersion());
        }

        private int RequestProtocolVersion()
        {
            // Sending a sink request to detect if the connected Crazyflie
            // supports protocol versioning
            var msg = new CrtpMessage((byte)CrtpPort.LINKCTRL, LINKSERVICE_SOURCE_CHANNEL,
                new byte[] { VERSION_GET_PROTOCOL_COMMAND } );
            _communicator.SendMessage(msg);
            if (!_waitForProtocolResult.WaitOne(5000))
            {
                _log.Warn("failed to retrieve protocol version (timeout)");
            }
            return ProtocolVersion;
        }

        private void PlatformMessageReceived(CrtpMessage message)
        {
            if (message.Channel == VERSION_COMMAND_CHANNEL &&
                message.Data[0] == VERSION_GET_PROTOCOL_COMMAND)
            {
                ProtocolVersion = message.Data[1];
                _log.Info("received protocol version: " + ProtocolVersion);
                _waitForProtocolResult.Set();
            }
        }

        private void CrtServiceMessageReceived(CrtpMessage message)
        {
            if (message.Channel == LINKSERVICE_SOURCE_CHANNEL)
            {
                // If the sink contains a magic string, get the protocol version,
                // otherwise -1
                if (message.Data.Length >= 18 && _encoder.GetString(message.Data.Take(18).ToArray()) ==
                    "Bitcraze Crazyflie")
                {
                    var msg = new CrtpMessage((byte)CrtpPort.PLATFORM, VERSION_COMMAND_CHANNEL,
                        new byte[] { 0 }); // get protocol = 0
                    _communicator.SendMessage(msg);
                }
                else
                {
                    _log.Info("not received Bitcraze Crazyflie from service message");
                    ProtocolVersion = -1;
                    _waitForProtocolResult.Set();
                }
            }
        }




    }
}
