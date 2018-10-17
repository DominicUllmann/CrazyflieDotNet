using CrazyflieDotNet.Crazyflie.Feature.Log;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using log4net;
using System;
using System.Linq;
using System.Collections.Generic;
using CrazyflieDotNet.Crazyflie.Feature.Common;
using System.IO;

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
    internal class Logger : TocContainerBase<LogTocElement>, ICrazyflieLogger
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

        private readonly IList<LogConfig> _blocks = new List<LogConfig>();
        private byte _config_id_counter = 1;


        internal Logger(ICrtpCommunicator communicator, bool useV2Protocol, DirectoryInfo cacheDirectory) : base(communicator, useV2Protocol,
            (byte)CrtpPort.LOGGING,
            new DirectoryInfo(Path.Combine(cacheDirectory.FullName, "LOG")))
        {
            _useV2Protocol = useV2Protocol;
            _communicator = communicator;
            _communicator.RegisterEventHandler((byte)CrtpPort.LOGGING, LogPacketReceived);
        }

        /// <summary>
        /// <see cref="ICrazyflieLogger.CreateEmptyLogConfigEntry"/>
        /// </summary>        
        public LogConfig CreateEmptyLogConfigEntry(string name, ushort period)
        {
            return new LogConfig(_communicator, this, name, period);
        }

        /// <summary>
        /// <see cref="ICrazyflieLogger.StartConfig(LogConfig)"/>
        /// </summary>        
        public void StartConfig(LogConfig config)
        {
            config.StartEnsureAdded();
        }

        public void StopConfig(LogConfig config)
        {
            config.Stop();
        }

        public void DeleteConfig(LogConfig config)
        {
            config.Delete();
        }

        protected override void StartLoadToc()
        {
            var message = new CrtpMessage((byte)CrtpPort.LOGGING,
                (byte)LogChannel.CHAN_SETTINGS, new byte[] { (byte)LogConfigCommand.CMD_RESET_LOGGING });
            _communicator.SendMessageExcpectAnswer(message, new byte[] { message.Data[0] });            
        }        

        /// <summary>
        /// See <see cref="ICrazyflieLogger.AddConfig(LogConfig)"/>.
        /// </summary>        
        public void AddConfig(LogConfig config)
        {
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
            if (size <= MAX_LOG_DATA_PACKET_SIZE)
            {
                config.Valid = true;
                config.UseV2 = _useV2Protocol;
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
                    "The log configuration is too large");
            }
        }

        private LogTocElement EnsureVariableInToc(LogConfig config, string name)
        {
            var tocVariable = CurrentToc.GetElementByCompleteName(name);
            if (tocVariable == null)
            {
                _log.Warn(
                    $"{name} not in TOC, this block cannot be used!");
                config.Valid = false;
                throw new InvalidOperationException($"variable {name} not in TOC");
            }

            return tocVariable;
        }

        internal LogConfig FindBlock(byte identifier)
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
            if (message.Channel == (byte)LogChannel.CHAN_SETTINGS)
            {
                var cmd = message.Data[0];
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
                var id = message.Data[0];
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
                        config.Added = true;
                        config.StartAlreadyAdded();
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

            if (CurrentToc == null)
            {
                _log.Debug("Logging reset, continue with TOC download");
                _blocks.Clear();

                FetchTocFromTocFetcher();
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

        /// <summary>
        /// <see cref="ICrazyflieLogger.IsLogVariableKnown(string)"/>
        /// </summary>
        public bool IsLogVariableKnown(string completeName)
        {
            if (CurrentToc == null)
            {
                throw new InvalidOperationException("no toc for log entries available.");
            }
            return CurrentToc.GetElementByCompleteName(completeName) != null;
        }
    }
}
