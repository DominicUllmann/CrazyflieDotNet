using CrazyflieDotNet.Crazyflie.Feature.Common;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CrazyflieDotNet.Crazyflie.Feature.Log
{

    public class LogDataReceivedEventArgs
    {
        private IDictionary<string, object> _logVariables = new Dictionary<string, object>();

        public uint TimeStamp { get; }

        public LogDataReceivedEventArgs(uint timeStamp) {
            TimeStamp = timeStamp;
        }
        
        internal void AddVariable(string name, object value)
        {
            _logVariables.Add(name, value);
        }

        public object GetVariable(string name)
        {
            return _logVariables[name];
        }
    }

    public delegate void LogDataReceivedEventHandler(object sender, LogDataReceivedEventArgs e);

    /// <summary>
    /// Representation of one log configuration that enables logging
    /// from the Crazyflie
    /// </summary>
    public class LogConfig
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(LogConfig));
        private ICrtpCommunicator _communicator;
        private ITocContainer<LogTocElement> _tocContainer;
        private readonly IList<LogVariable> _logVariables = new List<LogVariable>();
        private readonly IList<string> _defaultFetchAs = new List<string>();

        internal LogConfig(ICrtpCommunicator communicator, ITocContainer<LogTocElement> tocContainer, string name, byte period)
        {
            _communicator = communicator;
            _tocContainer = tocContainer;
            Period = period;
            Name = name;
        }

        public event LogDataReceivedEventHandler LogDataReceived;

        public ReadOnlyCollection<string> DefaultFetchAs
        {
            get
            {
                return new ReadOnlyCollection<string>(_defaultFetchAs);
            }
        }

        public ReadOnlyCollection<LogVariable> Variables
        {
            get
            {
                return new ReadOnlyCollection<LogVariable>(_logVariables);
            }
        }

        /// <summary>
        /// The identifier is assigned by the logger after adding the log config to
        /// the crazyfly.
        /// </summary>
        public byte? Identifier
        {
            get;
            set;
        }

        public string Name
        {
            get;
        }

        public bool Started {
            get;
            internal set;
        }

        internal bool UseV2 { get; set; }

        public byte ErrorNumber { get; internal set; }
        public bool Added { get; internal set; }
        public byte Period { get; internal set; }
        public bool Valid { get; internal set; }

        /// <summary>
        ///  Unpack received logging data so it represent real values according
        /// to the configuration in the entry
        /// </summary>
        public void UnpackLogData(byte[] logData, uint timestamp)
        {
            var eventData = new LogDataReceivedEventArgs(timestamp);
            var index = 0;
            foreach (var variable in _logVariables)
            {
                var targetSize = LogTocElement.GetSizeFromId(variable.FetchAsId);
                var toDecode = new byte[targetSize];
                Array.Copy(logData, index, toDecode, 0, targetSize);
                var value = LogTocElement.Unpack(variable.FetchAsId, toDecode);
                index += targetSize;
                eventData.AddVariable(variable.Name, value);
            }            
            LogDataReceived?.Invoke(this, eventData);
        }


    /// <summary>
    /// Add a new variable to the configuration
    /// </summary>        
    /// <param name="name">Complete name of the variable in the form group.name</param>
    /// <param name="fetchAs">
    ///  String representation of the type the variable should be
    ///  fetched as (i.e uint8_t, float, FP16, etc
    ///  
    ///  If no fetch_as type is supplied, then the stored as type will be used
    ///  (i.e the type of the fetched variable is the same as it's stored in the
    ///  Crazyflie)
    /// </param>
    public void AddVariable(string name, string fetchAs = null)
        {
            if (fetchAs != null)
            {
                _logVariables.Add(new LogVariable(name, fetchAs));
            }
            else
            {
                // We cannot determine the default type until we have connected. So
                // save the name and we will add these once we are connected.
                _defaultFetchAs.Add(name);
            }
        }

        /// <summary>
        /// Add a raw memory position to log
        /// </summary>
        /// <param name="name">Arbitrary name of the variable</param>
        /// <param name="fetchAs">
        /// String representation of the type of the data the memory
        /// should be fetch as (i.e uint8_t, float, FP16)
        /// </param>
        /// <param name="storedAs">
        /// String representation of the type the data is stored as
        /// in the Crazyflie
        /// </param>
        /// <param name="address">The address of the data</param>
        public void AddMemory(string name, string fetchAs, string storedAs, uint address)
        {
            _logVariables.Add(new LogVariable(name, fetchAs, storedAs, address));
        }

        /// <summary>
        /// Save the log configuration in the Crazyflie
        /// </summary>
        public void Create()
        {
            if (!Identifier.HasValue)
            {
                throw new InvalidOperationException("LogConfig not yet correctly added (no identifier).");
            }

            var messageBuilder = new MessageBuilder(                
                (byte)CrtpPort.LOGGING, (byte)Logger.LogChannel.CHAN_SETTINGS);
            if (UseV2)
            {
                messageBuilder.Add((byte)Logger.LogConfigCommand.CMD_CREATE_BLOCK_V2);
            }
            else
            {
                messageBuilder.Add((byte)Logger.LogConfigCommand.CMD_CREATE_BLOCK);
            }
            messageBuilder.Add(Identifier.Value);

            foreach (var variable in _logVariables)
            {
                var storage = variable.GetStorageAndFetchByte();
                if (!variable.IsTocVariable) // Memory location
                {                    
                    _log.Debug($"Logging to raw memory {storage} 0x{variable.Address.ToString("X")}");
                    messageBuilder.Add(storage);
                    messageBuilder.Add(variable.Address);
                }
                else
                {                    
                    var tocId = _tocContainer.CurrentLogToc.GetElementId(variable.Name);
                    _log.Debug($"Adding {variable.Name} with id={tocId} and type={storage}");
                    messageBuilder.Add(storage);
                    if (UseV2)
                    {
                        messageBuilder.Add((byte)(tocId & 0x0ff));
                        messageBuilder.Add((byte)((tocId >> 8) & 0x0ff));
                    }
                    else
                    {
                        messageBuilder.Add((byte)(tocId & 0x0ff));                        
                    }
                }
            }
            _log.Debug($"Adding log block id {Identifier}");
            _communicator.SendMessage(messageBuilder.Build());
            // TODO: expected reply: CMD_CREATE_BLOCK_(V2), self.id
        }

        /// <summary>
        /// Start the logging for this entry
        /// </summary>
        internal void Start()
        {
            if (!Added)
            {
                _log.Debug("First time block is started, add block");
                Create();
            }
            else
            {
                _log.Debug($"Block already registered, starting logging for id={Identifier}");
                _communicator.SendMessage(
                    new CrtpMessage((byte)CrtpPort.LOGGING,
                    (byte)Logger.LogChannel.CHAN_SETTINGS,
                    new byte[] { (byte)Logger.LogConfigCommand.CMD_START_LOGGING, Identifier.Value, Period }));
                // TODO: expected reply.
            }
        }

        /// <summary>
        /// Stop the logging for this entry
        /// </summary>
        public void Stop()
        {
            if (Identifier != null)
            {
                _log.Debug($"Sending stop logging for block id={Identifier}");
                _communicator.SendMessage(
                    new CrtpMessage((byte)CrtpPort.LOGGING,
                    (byte)Logger.LogChannel.CHAN_SETTINGS,
                    new byte[] { (byte)Logger.LogConfigCommand.CMD_STOP_LOGGING, Identifier.Value }));
                // TODO: expected reply.
            }
            else
            {
                _log.Warn("Stopping block, but no block registered");
            }            
        }

        /// <summary>
        /// Delete this entry in the Crazyflie
        /// </summary>
        public void Delete()
        {
            if (Identifier != null)
            {
                _log.Debug($"LogEntry: Sending delete logging for block id={Identifier}");
                _communicator.SendMessage(
                    new CrtpMessage((byte)CrtpPort.LOGGING,
                    (byte)Logger.LogChannel.CHAN_SETTINGS,
                    new byte[] { (byte)Logger.LogConfigCommand.CMD_DELETE_BLOCK, Identifier.Value }));
                // TODO: expected reply.
            }
            else
            {
                _log.Warn("Delete block, but no block registered");
            }
        }

    }
}
