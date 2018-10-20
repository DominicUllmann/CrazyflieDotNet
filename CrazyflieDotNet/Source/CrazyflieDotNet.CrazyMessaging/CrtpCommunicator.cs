using CrazyflieDotNet.CrazyMessaging.Protocol;
using CrazyflieDotNet.Crazyradio.Driver;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CrazyflieDotNet.CrazyMessaging
{

    public class LinkQualityEventArgs
    {
        public float LinkQuality { get; }

        public LinkQualityEventArgs(float linkQuality)
        {
            LinkQuality = linkQuality;
        }        
    }

    public delegate void LinkQualityEventHandler(object sender, LinkQualityEventArgs e);

    public class LinkErrorEventArgs
    {
        public string Message { get; }

        public LinkErrorEventArgs(string message)
        {
            Message = message;
        }
    }

    public delegate void LinkErrorEventHandler(object sender, LinkErrorEventArgs e);

    /// <summary>
    /// This class runs the message loops, which sends output messages and receives input messages.
    /// </summary>
    public class CrtpCommunicator : ICrtpCommunicator
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(CrtpCommunicator));

        private readonly Queue<CrtpMessage> _outgoing = new Queue<CrtpMessage>();
        private readonly Queue<CrtpMessage> _incoming = new Queue<CrtpMessage>();
        private readonly object _lock = new object();

        private readonly ICrazyradioDriver _crazyradioDriver;
        private Thread _communicationThread;
        private Thread _eventLoopThread;
        private bool _isRunning;
        private bool _safeLink;
        private byte _curr_up = 0;
        private byte _curr_down = 1;
        private ManualResetEventSlim _waitForOutqueue = new ManualResetEventSlim(false);
        private ManualResetEventSlim _waitForInqueue = new ManualResetEventSlim(false);

        private ManualResetEvent _comStarted = new ManualResetEvent(false);
        private ManualResetEvent _eventLoopStarted = new ManualResetEvent(false);

        private readonly EventRegistry _crtpEventRegistry = new EventRegistry();
        private readonly AtLeastOnceCommunicatorStrategy _atLeastOnceCommunicatorStrategy;

        private Queue<int> _retries = new Queue<int>();
        private int _retry_sum = 0;
        private byte _retryBeforeDisconnect;

        private byte _emptyCounter;
        private int _waitTime;

        private const int _maxInqueue = 100;
        private const int _maxOutqueue = 20;

        public CrtpCommunicator(ICrazyradioDriver crazyradioDriver)
        {
            _crazyradioDriver = crazyradioDriver;
            _atLeastOnceCommunicatorStrategy = new AtLeastOnceCommunicatorStrategy(this);
        }

        public event LinkQualityEventHandler LinkQuality;
        public event LinkErrorEventHandler LinkError;
        /// <summary>
        /// The number of retries before informing about a link error via the LinkError event.
        /// </summary>
        public byte NumberOfRetries { get; set; } = 100;

        public void Start()
        {
            if (!_crazyradioDriver.IsOpen || _crazyradioDriver.Channel == null)
            {
                throw new InvalidOperationException("please open the radio driver first and select channel");
            }

            if (_communicationThread != null || _eventLoopThread != null)
            {
                throw new InvalidOperationException("please stop first before trying to start again.");
            }

            _isRunning = true;
            try
            {
                _eventLoopStarted.Reset();
                _eventLoopThread = new Thread(new ThreadStart(EventLoopSafe));
                _eventLoopThread.Start();

                if (!_eventLoopStarted.WaitOne(5000))
                {
                    throw new InvalidOperationException("event thread start failed");
                }

                _comStarted.Reset();
                _communicationThread = new Thread(new ThreadStart(CommunicationLoopSafe));
                _communicationThread.Start();

                if (!_comStarted.WaitOne(5000))
                {                    
                    throw new InvalidOperationException("communication thread start failed");
                }
            }
            catch
            {
                _isRunning = false;
            }
        }

        private void CommunicationLoopSafe()
        {
            try
            {
                CommunicationLoop();
            }
            catch (Exception ex)
            {
                _log.Error("exception in communication loop thread.", ex);
                LinkError?.Invoke(this, new LinkErrorEventArgs("unexpected error in communication loop: " + ex));
            }
            _communicationThread = null;
        }

        private void EventLoopSafe()
        {
            try
            {
                EventLoop();
            }
            catch (Exception ex)
            {
                _log.Error("exception in event loop thread.", ex);
                LinkError?.Invoke(this, new LinkErrorEventArgs("unexpected error in event loop: " + ex));
            }
            _eventLoopThread = null;
        }

        public void Stop()
        {
            if (_communicationThread == null || _eventLoopThread == null)
            {
                throw new InvalidOperationException("please start first before trying to start again.");
            }
            var toWait = new[] { _communicationThread, _eventLoopThread };
            _isRunning = false;
            foreach (var wait in toWait)
            {
                if (!wait.Join(4000))
                {
                    _log.Error("Failed to stop communicator threads");
                }
            }
        }

        public void SendMessage(CrtpMessage message)
        {            
            lock (_lock)
            {
                _log.Debug($"enqueue message for port {message.Port} / channel {message.Channel}; number of messages waiting: {_outgoing.Count}");
                // ensure that outqueue doesn't get too big. Drop older packets if more than one already waiting
                // so that we have at most 100 packets in queue.
                while (_outgoing.Count > _maxOutqueue)
                {                    
                    // the older messages are at the beginning, so dequeue until we have the most recent on top.
                    var old = _outgoing.Dequeue();
                    _log.Warn($"too many messages in out queue, drop for port {old.Port}");
                }
                _outgoing.Enqueue(message);
                _waitForOutqueue.Set();
            }            
        }

        private void CommunicationLoop()
        {
            _retryBeforeDisconnect = NumberOfRetries;
            _emptyCounter = 0;
            _waitTime = 0;

            var emptyMessage = new CrtpMessage(0xff, new byte[0]);
            var outMessage = emptyMessage;

            // Try up to 10 times to enable the safelink mode                
            TryEnableSafeLink();

            _comStarted.Set();
            _log.Info("Communication with crazyfly started.");

            while (_isRunning)
            {

                CrtpResponse response = null;
                try
                {
                    response = Send(outMessage);
                }
                catch (Exception ex)
                {
                    _log.Error("error sending message", ex);
                    LinkError?.Invoke(this, new LinkErrorEventArgs("failed to send: " + ex));
                }

                // Analyse the in data packet ...
                if (response == null)
                {
                    _log.Info("Dongle reported ACK status == None");
                    continue;
                }

                TrackLinkQuality(response);
                if (!response.Ack)
                {
                    UpdateRetryCount();
                    if (_retryBeforeDisconnect > 0)
                    {
                        continue;
                    }
                    // else try a next packet to send.
                }
                _retryBeforeDisconnect = NumberOfRetries;

                // after we managed to send the message, set the next one to the ping message again.
                outMessage = emptyMessage;

                if (response.HasContent)
                {
                    _waitTime = 0;
                    _emptyCounter = 0;
                    lock (_lock)
                    {
                        _log.Debug($"incoming queue count: {_incoming.Count}; enqueue for {response.Content.Port} / {response.Content.Channel}");
                        _incoming.Enqueue(response.Content);
                        while (_incoming.Count > _maxInqueue)
                        {                            
                            // dequue old messages which are not processed and therefore stale.
                            var old = _incoming.Dequeue();
                            _log.Warn($"Too many old message not processed, drop for port: {old.Port}.");
                        }
                        _waitForInqueue.Set();
                    }
                }
                else
                {
                    _emptyCounter += 1;
                    if (_emptyCounter > 10)
                    {
                        _emptyCounter = 10;
                        // Relaxation time if the last 10 packet where empty
                        _waitTime = 10;
                    }
                    else
                    {
                        // send more ack messages to get more responses and don't wait for 
                        // user out messages; start waiting only after 10 empty messages received.
                        _waitTime = 0;
                    }
                }

                _waitForOutqueue.Wait(_waitTime);
                lock (_lock)
                {
                    if (_outgoing.Count > 0)
                    {
                        outMessage = _outgoing.Dequeue();
                    }

                    if (_outgoing.Count == 0)
                    {
                        _waitForOutqueue.Reset();
                    }
                }
            }
            _log.Debug("send loop ended");
        }

        private void TryEnableSafeLink()
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    byte[] response = _crazyradioDriver.SendData(new byte[] { 0xff, 0x05, 0x01 });
                    if (response.Length == 4 &&
                        response[1] == 0xff && response[2] == 0x05 && response[3] == 0x01)
                    {
                        _safeLink = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("error sending safelink message", ex);
                }
            }
        }

        private void UpdateRetryCount()
        {
            if (_retryBeforeDisconnect > 0)
            {
                _retryBeforeDisconnect -= 1;
            }
            if (_retryBeforeDisconnect == 0)
            {
                LinkError?.Invoke(this, new LinkErrorEventArgs("Too many packets lost"));
            }
        }

        private void TrackLinkQuality(CrtpResponse response)
        {
            var retry = 10 - response.Retry;
            _retries.Enqueue(retry);
            _retry_sum += retry;
            while (_retries.Count > 100)
            {
                var oldest = _retries.Dequeue();
                _retry_sum -= oldest;
            }
            var linkQuality = ((float)_retry_sum / _retries.Count) * 10;
            LinkQuality?.Invoke(this, new LinkQualityEventArgs(linkQuality));
        }

        private CrtpResponse Send(CrtpMessage outMessage)
        {
            var content = outMessage.Message;
            // Adds 1bit counter to CRTP header to guarantee that no ack(downlink)
            // payload are lost and no uplink packet are duplicated.
            // The caller should resend packet if not acked(ie.same as with a
            // direct call to crazyradio.send_packet)
            if (_safeLink)
            {
                content[0] &= 0xF3;
                content[0] |= (byte)(_curr_up << 3 | _curr_down << 2);
            }
            var result = new CrtpResponse(_crazyradioDriver.SendData(content));
            if (_safeLink) {
                if (result.Ack && ((result.Content.Header & 0x04) == (_curr_down << 2)))
                {
                    _curr_down = (byte)(1 - _curr_down);
                }
                if (result.Ack)
                {
                    _curr_up = (byte)(1 - _curr_up);
                }
            }
            return result;
        }

        // the event loop listen on the incoming messages and processed them.
        private void EventLoop()
        {
            _eventLoopStarted.Set();
            while (_isRunning)
            {
                _waitForInqueue.Wait(1);

                CrtpMessage inMessage = null;
                lock (_lock)
                {
                    if (_incoming.Count > 0)
                    {
                        inMessage = _incoming.Dequeue();
                        _log.Debug($"Dequeued message for {inMessage.Channel} / {inMessage.Port}");
                    }
                    if (_incoming.Count == 0)
                    {
                        _waitForInqueue.Reset();
                    }
                }

                if (inMessage == null)
                {
                    continue;
                }

                _crtpEventRegistry.Notify(inMessage);
            }
            _log.Debug("Event loop ended");
        }

        public void SendMessageExcpectAnswer(CrtpMessage message, byte[] startResponseContent)
        {
            SendMessageExcpectAnswer(message, startResponseContent, TimeSpan.FromMilliseconds(250));
        }        

        public void SendMessageExcpectAnswer(CrtpMessage message, Func<CrtpMessage, bool> isExpectedResponse, TimeSpan timeout)
        {
            _atLeastOnceCommunicatorStrategy.RegisterExpectation(message, isExpectedResponse, timeout);
            SendMessage(message);
        }

        public void SendMessageExcpectAnswer(CrtpMessage message, byte[] startResponseContent, TimeSpan timeout)
        {
            SendMessageExcpectAnswer(message, CheckMatch(message, startResponseContent), timeout);
        }

        private Func<CrtpMessage, bool> CheckMatch(CrtpMessage messageToSend, byte[] startResponseContent)
        {
            return message =>
            {
                if (message.Port != messageToSend.Port ||
                    message.Channel != messageToSend.Channel ||
                    message.Data.Length < startResponseContent.Length)
                {
                    return false;
                }

                for (int i = 0; i < startResponseContent.Length; i++)
                {
                    if (startResponseContent[i] != message.Data[i])
                    {
                        return false;
                    }
                }

                return true;
            };
        }

        public void RegisterEventHandler(byte port, CrtpEventCallback crtpEventCallback)
        {
            _crtpEventRegistry.RegisterEventHandler(port, crtpEventCallback);
        }

        public void RegisterAllEventHandler(CrtpEventCallback crtpEventCallback)
        {
            _crtpEventRegistry.RegisterAllEventHandler(crtpEventCallback);
        }

        public void RemoveEventHandler(byte port, CrtpEventCallback crtpEventCallback)
        {
            _crtpEventRegistry.RemoveEventHandler(port, crtpEventCallback);
        }
    }
}
