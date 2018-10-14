using CrazyflieDotNet.Crazyflie.Feature.Log;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using log4net;
using System;
using System.Linq;
using System.Collections.Generic;

namespace CrazyflieDotNet.Crazyflie.Feature
{

    /// <summary>
    /// 
    /// Enables logging of variables from the Crazyflie.
    ///
    /// When a Crazyflie is connected it's possible to download a TableOfContent of all
    /// the variables that can be logged. Using this it's possible to add logging
    /// configurations where selected variables are sent to the client at a
    /// specified period.
    ///
    /// Terminology:
    ///  Log configuration - A configuration with a period and a number of variables
    ///                      that are present in the TOC.
    ///  Stored as         - The size and type of the variable as declared in the
    ///                      Crazyflie firmware
    ///  Fetch as          - The size and type that a variable should be fetched as.
    ///                      This does not have to be the same as the size and type
    ///                      it's stored as.    
    /// States of a configuration:
    ///   Created on host - When a configuration is created the contents is checked
    ///                     so that all the variables are present in the TOC. If not
    ///                     then the configuration cannot be created.
    ///   Created on CF   - When the configuration is deemed valid it is added to the
    ///                     Crazyflie. At this time the memory constraint is checked
    ///                     and the status returned.
    ///   Started on CF   - Any added block that is not started can be started.Once
    ///                     started the Crazyflie will send back logdata periodically
    ///                     according to the specified period when it's created.
    ///   Stopped on CF   - Any started configuration can be stopped. The memory taken
    ///                     by the configuration on the Crazyflie is NOT freed, the
    ///                     only effect is that the Crazyflie will stop sending
    ///                     logdata back to the host.
    ///   Deleted on CF   - Any block that is added can be deleted. When this is done
    ///                     the memory taken by the configuration is freed on the
    ///                     Crazyflie. The configuration will have to be re-added to
    ///                     be used again.
    /// </summary>
    public class Logger : ITocContainer
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Logger));

        /// <summary>
        ///  Channels used for the logging port
        /// </summary>
        internal enum LogChannel : byte
        {
            CHAN_TOC = 0,
            CHAN_SETTINGS = 1,
            CHAN_LOGDATA = 2,
        }

        /// <summary>
        /// Commands used when accessing the Log configurations
        /// </summary>
        internal enum LogConfigCommand : byte
        {
            CMD_CREATE_BLOCK = 0,
            CMD_APPEND_BLOCK = 1,
            CMD_DELETE_BLOCK = 2,
            CMD_START_LOGGING = 3,
            CMD_STOP_LOGGING = 4,
            CMD_RESET_LOGGING = 5,
            CMD_CREATE_BLOCK_V2 = 6,
            CMD_APPEND_BLOCK_V2 = 7,
        }

        /// <summary>
        /// Possible states when receiving TOC
        /// </summary>
        private enum TocState
        {
            IDLE,
            GET_TOC_INF,
            GET_TOC_ELEMENT,
        }

        // These codes can be decoded using os.stderror in python, but
        // some of the text messages will look very strange
        // in the UI, so they are redefined here

        private const byte ENOMEM = 12;
        private const byte ENOEXEC = 8;
        private const byte ENOENT = 2;
        private const byte E2BIG = 7;
        private const byte EEXIST = 17;

        private static IDictionary<byte, string> _err_codes = new Dictionary<byte, string>()
        {
            { ENOMEM, "No more memory available" },
            { ENOEXEC, "Command not found" },
            { ENOENT, "No such block id" },
            { E2BIG, "Block too large" },
            { EEXIST, "Block already exists" }
        };


        // The max size of a CRTP packet payload
        private const byte MAX_LOG_DATA_PACKET_SIZE = 30;

        private ICrtpCommunicator _communicator;
        private readonly IList<LogConfig> _blocks = new List<LogConfig>();
        private LogToc _toc = null;
        private LogTocCache _tocCache = new LogTocCache();
        private bool _useV2;
        private byte _config_id_counter = 1;

        public LogToc CurrentLogToc => _toc;

        public Logger(ICrtpCommunicator communicator)
        {
            _communicator = communicator;
            _communicator.RegisterEventHandler((byte)CrtpPort.LOGGING, LogPacketReceived);
        }

        public void RefreshToc()
        {
            // TODO: self.cf.platform.get_protocol_version() >= 4
            _useV2 = (_communicator.ProtocolVersion >= 4);
            _toc = null;

            // TODO
            // self._refresh_callback = refresh_done_callback

            var message = new CrtpMessage((byte)CrtpPort.LOGGING,
                (byte)LogChannel.CHAN_SETTINGS, new byte[] { (byte)LogConfigCommand.CMD_RESET_LOGGING });
            _communicator.SendMessage(message);
            // TODO: expected reply

        }

        /// <summary>
        /// Add a log configuration to the logging framework.
        ///
        /// When doing this the contents of the log configuration will be validated
        /// and listeners for new log configurations will be notified. When
        /// validating the configuration the variables are checked against the TOC
        /// to see that they actually exist.If they don't then the configuration
        /// cannot be used.Since a valid TOC is required, a Crazyflie has to be
        /// connected when calling this method, otherwise it will fail.
        /// </summary>        
        public void AddConfig(LogConfig config)
        {
            // TODO: check connected
            // if not self.cf.link:
            // logger.error('Cannot add configs without being connected to a '
            //              'Crazyflie!')
            // return

            // If the log configuration contains variables that we added without
            // type (i.e we want the stored as type for fetching as well) then
            // resolve this now and add them to the block again.
            foreach (var name in config.DefaultFetchAs)
            {
                LogTocElement tocVariable = EnsureVariableInToc(config, name);

                // Now that we know what type this variable has, add it to the log
                // config again with the correct type
                config.AddVariable(name, tocVariable.CType);
            }

            // Now check that all the added variables are in the TOC and that
            // the total size constraint of a data packet with logging data is
            // not

            var size = 0;
            foreach (var variable in config.Variables)
            {
                size += LogTocElement.GetSizeFromId(variable.FetchAsId);
                // Check that we are able to find the variable in the TOC so
                // we can return error already now and not when the config is sent
                if (variable.IsTocVariable)
                {
                    EnsureVariableInToc(config, variable.Name);
                }
            }

            if (size <= MAX_LOG_DATA_PACKET_SIZE && (config.Period > 0 && config.Period < 0xFF))
            {
                config.Valid = true;
                // TODO connect to CF
                // config.CF = ...
                // config.UseV2 = _useV2
                config.Identifier = _config_id_counter;
                _config_id_counter = (byte)((_config_id_counter + 1) % 255);
                _blocks.Add(config);
                // TODO: event
                //self.block_added_cb.call(logconf)
            }
            else
            {
                config.Valid = false;
                throw new ArgumentException(
                    "The log configuration is too large or has an invalid parameter");
            }
        }

        private LogTocElement EnsureVariableInToc(LogConfig config, string name)
        {
            var tocVariable = _toc.GetElementByCompleteName(name);
            if (tocVariable == null)
            {
                _log.Warn(
                    $"{name} not in TOC, this block cannot be used!");
                config.Valid = false;
                throw new InvalidOperationException($"variable {name} not in TOC");
            }

            return tocVariable;
        }

        public LogConfig FindBlock(byte identifier)
        {
            foreach (var block in _blocks)
            {
                if (block.Identifier == identifier)
                {
                    return block;
                }
            }
            return null;
        }

        /// <summary>
        /// Callback for newly arrived packets with TOC information
        /// </summary>
        private void LogPacketReceived(CrtpMessage message)
        {
            var cmd = message.Data[0];
            if (message.Channel == (byte)LogChannel.CHAN_SETTINGS)
            {
                var id = message.Data[1];
                var block = FindBlock(id);
                var errorStatus = message.Data[2];
                if (cmd == (byte)LogConfigCommand.CMD_CREATE_BLOCK ||
                    cmd == (byte)LogConfigCommand.CMD_CREATE_BLOCK_V2)
                {
                    HandleCreateBlock(id, block, errorStatus);
                }
                else if (cmd == (byte)LogConfigCommand.CMD_START_LOGGING)
                {
                    HandleStartLoggingCommand(id, block, errorStatus);
                }
                else if (cmd == (byte)LogConfigCommand.CMD_STOP_LOGGING)
                {
                    HandleStopLoggingCommand(id, block, errorStatus);
                }
                else if (cmd == (byte)LogConfigCommand.CMD_DELETE_BLOCK)
                {
                    HandleDeleteLoggingCommand(id, block, errorStatus);
                }
                else if (cmd == (byte)LogConfigCommand.CMD_RESET_LOGGING)
                {
                    HandleResetLoggingCommand();
                }
            }
            if (message.Channel == (byte)LogChannel.CHAN_LOGDATA)
            {
                var id = message.Data[1];
                var block = FindBlock(id);
                HandleReceivedLogData(id, block, message.Data.Skip(1).ToArray());
            }
        }

        private void HandleCreateBlock(byte id, LogConfig config, byte errorStatus)
        {
            if (config != null)
            {
                if (errorStatus == 0 || errorStatus == EEXIST)
                {
                    if (!config.Added)
                    {
                        _log.Debug($"Have successfully added id={id}");
                        var msg = new CrtpMessage((byte)CrtpPort.LOGGING,
                                                  (byte)LogChannel.CHAN_SETTINGS,
                                                  new byte[] {
                                                      (byte)LogConfigCommand.CMD_START_LOGGING, id,
                                                      config.Period
                                                    }
                                                  );
                        _communicator.SendMessage(msg);
                        // TODO: expected_reply: CMD_START_LOGGING, id)                        
                        config.Added = true;
                    }
                }
                else
                {
                    LogErrorStatus(id, errorStatus, "adding");
                    config.ErrorNumber = errorStatus;
                    // TODO:
                    //block.added_cb.call(False)
                    //block.error_cb.call(block, msg)
                }
            }
            else
            {
                _log.Warn($"No LogEntry to assign block to; id: {id}");
            }
        }

        private void HandleReceivedLogData(byte id, LogConfig config, byte[] logData)
        {
            var timestamp = (uint)(logData[0] | logData[1] << 8 | logData[2] << 16);
            logData = logData.Skip(3).ToArray();
            if (config != null)
            {
                config.UnpackLogData(logData, timestamp);
            }
            else
            {
                _log.Warn($"Error no LogEntry to handle id={id}");
            }
        }

        private void HandleResetLoggingCommand()
        {
            // Guard against multiple responses due to re-sending

            if (_toc == null)
            {
                _log.Debug("Logging reset, continue with TOC download");
                _blocks.Clear();

                var tocFetcher = new LogTocFetcher(_communicator, _tocCache);
                _toc = tocFetcher.Start();
            }
        }

        private void HandleDeleteLoggingCommand(byte id, LogConfig config, byte errorStatus)
        {
            // Accept deletion of a block that isn't added. This could
            // happen due to timing (i.e add/start/delete in fast sequence)
            if (errorStatus == 0x00 || errorStatus == ENOENT)
            {
                _log.Info($"Have successfully deleted id={id}");
                if (config != null)
                {
                    config.Started = false;
                    config.Added = false;
                }
            }
        }

        private void HandleStopLoggingCommand(byte id, LogConfig config, byte errorStatus)
        {
            if (errorStatus == 0x00)
            {
                _log.Info($"Have successfully stopped logging for id={id}");
                if (config != null)
                {
                    config.Started = false;
                }
            }
        }

        private void HandleStartLoggingCommand(byte id, LogConfig config, byte errorStatus)
        {
            if (errorStatus == 0x00)
            {
                _log.Info($"Have successfully started logging for id={id}");
                if (config != null)
                {
                    config.Started = true;
                }
            }
            else
            {
                LogErrorStatus(id, errorStatus, "starting");

                if (config != null)
                {
                    config.ErrorNumber = errorStatus;
                    // TODO:
                    //block.started_cb.call(self, False)
                }
            }
        }

        private void LogErrorStatus(byte id, byte errorStatus, string action)
        {
            var msg = "unkown error";
            _err_codes.TryGetValue(errorStatus, out msg);
            _log.Warn($"Error {errorStatus} when {action} id={id}; {msg}");
        }
    }
}
