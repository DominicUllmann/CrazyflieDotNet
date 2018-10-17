using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using log4net;

namespace CrazyflieDotNet.Crazyflie.Feature.Param
{

    internal class ParameterReceivedEventArgs
    {
        public ushort Id { get; }
        public byte[] ParamValue { get; }

        public ParameterReceivedEventArgs(ushort id, byte[] paramValue)
        {
            Id = id;
            ParamValue = paramValue;
        }
    }

    internal class ParameterStoredEventArgs
    {
        public ushort Id { get; }        

        public ParameterStoredEventArgs(ushort id)
        {
            Id = id;
        }
    }

    internal delegate void ParameterReceivedEventHandler(object sender, ParameterReceivedEventArgs e);
    internal delegate void ParameterStoredEventHandler(object sender, ParameterStoredEventArgs e);

    /// <summary>
    /// The parameter store is an abstraction of the stored parameters on the crazyflie.
    /// It allows to retrieve current values of parameters as well as set new values.
    /// It uses a queue behind to ensure that no requests are lost and to ensure that they are performed sequentially.
    /// </summary>
    internal class ParameterSynchronizer
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(ParameterSynchronizer));

        private class ParameterRequest
        {
            public ushort ForParamId { get; }
            public CrtpMessage RequestMessage { get; }

            public byte[] VerifciationBytes { get; }
            

            public ParameterRequest(ushort forParamId, CrtpMessage requestMessage, byte[] verificationBytes)
            {
                ForParamId = forParamId;
                RequestMessage = requestMessage;
                VerifciationBytes = verificationBytes;
            }
        }

        private readonly ICrtpCommunicator _communicator;
        private readonly object _lock = new object();
        private readonly Queue<ParameterRequest> _requests = new Queue<ParameterRequest>();        
        private readonly bool _useV2;
        private Thread _synchronizationThread;
        private bool _isRunning;
        private ManualResetEvent _waitForContent = new ManualResetEvent(false);
        private ManualResetEvent _waitForResponse = new ManualResetEvent(false);
        public event ParameterReceivedEventHandler ParameterReceived;
        public event ParameterStoredEventHandler ParameterStored;

        internal ParameterSynchronizer(ICrtpCommunicator communicator, bool useV2)
        {
            _communicator = communicator;
            _useV2 = useV2;
            _communicator.RegisterEventHandler((byte) CrtpPort.PARAM, ParamMessageReceived);                        
        }
        
        public void StartProcessing()
        {            
            _synchronizationThread = new Thread(ProcessQueue);
            _isRunning = true;
            _waitForContent.Reset();
            _waitForResponse.Reset();
            _synchronizationThread.Start();
        }

        public void StopProcessing()
        {
            lock (_lock)
            {
                _isRunning = false;
                _waitForContent.Set(); // stop wait to ensure that we see isRunning change.            
                _waitForResponse.Set(); // stop wait to ensure that we see isRunning change.
            }
            if (!_synchronizationThread.Join(2000))
            {
                throw new ApplicationException("failed to stop parameter synchronization thread.");
            }
        }

        private void ProcessQueue()
        {
            while (_isRunning)
            {
                try
                {
                    if (_waitForContent.WaitOne())
                    {
                        lock(_lock)
                        {
                            if (_requests.Any())
                            {
                                _waitForResponse.Reset(); // ensure that thread waites for response.
                                var currentRequest = _requests.Peek();
                                _communicator.SendMessageExcpectAnswer(currentRequest.RequestMessage, currentRequest.VerifciationBytes);
                            }
                            else
                            {
                                // ensure that thread pauses until next request arrives.
                                _waitForContent.Reset();
                                continue; // ensure that we don't wait for a reponse for a non-existing request
                            }
                            if (!_isRunning)
                            {
                                break;
                            }
                        }                        
                        // only send next request after we received a reponse.
                        _waitForResponse.WaitOne();
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("failed to process parameter request", ex);
                }
            }
            
        }

        public void StoreParamValue(ushort id, byte[] encodedValue)
        {
            _log.Debug($"Requesting to set param {id}");

            var messageBuilder = new MessageBuilder((byte)CrtpPort.PARAM, (byte)ParamConfigurator.ParamChannel.WRITE_CHANNEL);
            int expectedReplyLength;
            if (_useV2)
            {
                messageBuilder.Add(id);
                expectedReplyLength = 2;
            }
            else
            {
                messageBuilder.Add((byte)(id & 0xFF));
                expectedReplyLength = 1;
            }
            messageBuilder.Add(encodedValue);

            var msg = messageBuilder.Build();

            lock (_lock)
            {
                _requests.Enqueue(new ParameterRequest(id, msg, msg.Data.Take(expectedReplyLength).ToArray()));
                _waitForContent.Set();
            }
        }

        /// <summary>
        /// Place a param update request on the queue
        /// </summary>
        public void RequestLoadParamValue(ushort id)
        {
            _log.Debug($"Requesting read param value {id}");
            var messageBuilder = new MessageBuilder((byte) CrtpPort.PARAM, (byte) ParamConfigurator.ParamChannel.READ_CHANNEL);
            if (_useV2)
            {
                messageBuilder.Add(id);
            }
            else
            {
                messageBuilder.Add((byte) (id & 0xFF));
            }
            
            var msg = messageBuilder.Build();
            lock (_lock)
            {
                _requests.Enqueue(new ParameterRequest(id, msg, msg.Data.Take(msg.Data.Length).ToArray()));
                _waitForContent.Set();
            }
        }

        /// <summary>
        /// Callback for newly arrived packets
        /// </summary>
        private void ParamMessageReceived(CrtpMessage message)
        {
            _log.Debug($"received parameter value result message for channel {message.Channel}");
            if (message.Channel == (byte)ParamConfigurator.ParamChannel.READ_CHANNEL ||
                message.Channel == (byte)ParamConfigurator.ParamChannel.WRITE_CHANNEL)
            {
                ParameterReceivedEventArgs notificationReceived = null;
                ParameterStoredEventArgs notificationStored = null;
                lock (_lock)
                {                    
                    ushort forId = (_useV2 ? BitConverter.ToUInt16(message.Data.Take(2).ToArray(), 0) : message.Data.First());
                    _log.Debug($"Check for existing requests for received parameter value for id {forId}.");
                    if (_requests.Any())
                    {                        
                        var request = _requests.Peek();
                        if (request.ForParamId == forId && request.RequestMessage.Channel == message.Channel)
                        {
                            // remove now from the queue and notify processing thread to take next one.
                            _log.Debug($"Dequeue current parameter request as fullfilled. Id: {forId}, channel: {message.Channel}");
                            _requests.Dequeue();
                            _waitForResponse.Set();
                        } 

                        if (message.Channel == (byte)ParamConfigurator.ParamChannel.READ_CHANNEL)
                        {
                            _log.Info($"received parameter value result for param {forId}");
                            // for version 2, it seems that we need to skip 3 bytes (2 for id, 1 for something else).
                            notificationReceived = new ParameterReceivedEventArgs(forId, message.Data.Skip(_useV2 ? 3 : 1).ToArray());
                        }
                        if (message.Channel == (byte)ParamConfigurator.ParamChannel.WRITE_CHANNEL)
                        {
                            _log.Info($"stored parameter value result for param {forId}");                            
                            notificationStored = new ParameterStoredEventArgs(forId);
                        }
                    }
                    else
                    {
                        _log.Warn($"no macthing request found for parameter {forId}");
                    }
                }
                if (notificationReceived != null)
                {
                    ParameterReceived?.Invoke(this, notificationReceived);
                }
                if (notificationStored != null)
                {
                    ParameterStored?.Invoke(this, notificationStored);
                }
            }
        }
    }
}