﻿using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.CrazyMessaging.Protocol;
using log4net;
using System;
using System.Linq;

namespace CrazyflieDotNet.Crazyflie.Feature.Common
{

    public class TocFetchedEventArgs
    {        
    }

    public delegate void TocReceivedEventHandler(object sender, TocFetchedEventArgs e);

    internal class TocFetcher<T> 
        where T : ITocElement, new()
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(TocFetcher<T>));
        private const byte TOC_CHANNEL = 0;

        /// <summary>
        /// Commands used when accessing the Table of Contents
        /// </summary>
        private enum TocCommand : byte
        {
            CMD_TOC_ELEMENT = 0, // original version: up to 255 entries
            CMD_TOC_INFO = 1,    // original version: up to 255 entries
            CMD_TOC_ITEM_V2 = 2,  // version 2: up to 16k entries
            CMD_TOC_INFO_V2 = 3  // version 2: up to 16k entries
        }

        private enum FetchState
        {
            NONE,
            GET_TOC_INFO,
            GET_TOC_ELEMENT
        }

        private ICrtpCommunicator _communicator;
        private FetchState _fetchState;
        private bool _useV2;
        private readonly byte _port;
        private Toc<T> _toc;
        private TocCache<T> _tocCache;
        private ushort _requestedIndex;
        private ushort _nbrOfItems;
        private uint _crc;

        internal TocFetcher(ICrtpCommunicator communicator, TocCache<T> cache, byte port, bool useV2protocol)
        {
            _communicator = communicator;            
            _port = port;
            _tocCache = cache;
            _useV2 = useV2protocol;
        }

        internal event TocReceivedEventHandler TocReceived;

        /// <summary>
        /// returns the toc which is asynchronously completed.
        /// </summary>        
        internal Toc<T> Start()
        {            
            _toc = new Toc<T>();
            _fetchState = FetchState.GET_TOC_INFO;
            _communicator.RegisterEventHandler(_port, TocPacketReceived);
            SendTocInfoRequest();
            return _toc;
        }

        private void SendTocInfoRequest()
        {
            byte[] content;
            if (_useV2)
            {
                content = new byte[] { (byte)TocCommand.CMD_TOC_INFO_V2 };
            }
            else
            {
                content = new byte[] { (byte)TocCommand.CMD_TOC_INFO };
            }

            var msg = new CrtpMessage(_port, TOC_CHANNEL,
                content);
            _communicator.SendMessageExcpectAnswer(msg, new [] { msg.Data[0] });            
        }

        /// <summary>
        /// Handle a newly arrived packet
        /// </summary>        
        private void TocPacketReceived(CrtpMessage message)
        {
            if (message.Channel != TOC_CHANNEL)
            {
                return;
            }
            var payload = message.Data.Skip(1).ToArray();
            if (_fetchState == FetchState.GET_TOC_INFO)
            {
                HandleGetTocInfo(payload);
            }
            else if (_fetchState == FetchState.GET_TOC_ELEMENT)
            {
                // Always add new element, but only request new if it's not the
                // last one.
                HandleGetTocElement(payload);
            }
        }

        private void HandleGetTocInfo(byte[] payload)
        {      
            if (_useV2)
            {
                _nbrOfItems = BitConverter.ToUInt16(payload.Take(2).ToArray(), 0);
                payload = payload.Skip(2).ToArray();
            }
            else
            {
                _nbrOfItems = payload[0];
                payload = payload.Skip(1).ToArray();
            }
            _crc = BitConverter.ToUInt32(payload, 0);

            _log.Debug($"Got TOC CRC, {_nbrOfItems} items and crc=0x{_crc.ToString("X")}");

            var cached = _tocCache.GetByCrc(_crc);
            if (cached != null)
            {
                _toc.AddFromCache(cached);
                _log.Info($"TOC found in cache with crc {_crc} ");
                TocFetchFinished();
            } 
            else
            {
                _fetchState = FetchState.GET_TOC_ELEMENT;
                _requestedIndex = 0;
                RequestTocElement(_requestedIndex);
            }                       
        }

        private void HandleGetTocElement(byte[] payload)
        {
            ushort index;
            if (_useV2)
            {
                index = BitConverter.ToUInt16(payload.Take(2).ToArray(), 0);
            }
            else
            {
                index = payload[0];
            }

            if (index != _requestedIndex)
            {
                return;
            }
            byte[] tocContent;
            if (_useV2)
            {
                tocContent = payload.Skip(2).ToArray();
            }
            else
            {
                tocContent = payload.Skip(1).ToArray();
            }
            var element = new T();
            element.InitializeFrom(index, tocContent);
            _toc.AddElement(element);
            _log.Debug($"Added element {index} to toc");

            if (_requestedIndex < (_nbrOfItems -1))
            {
                _log.Debug($"More variables, requesting index {_requestedIndex + 1}");
                _requestedIndex++;
                RequestTocElement(_requestedIndex);
            }
            else // No more variables in TOC
            {
                _tocCache.AddToc(_crc, _toc);
                TocFetchFinished();
            }
        }

        /// <summary>
        /// Callback for when the TOC fetching is finished
        /// </summary>
        private void TocFetchFinished()
        {
            _communicator.RemoveEventHandler(_port, TocPacketReceived);
            _log.Debug("Fetch Toc completed");
            TocReceived?.Invoke(this, new TocFetchedEventArgs());
        }

        /// <summary>
        /// Request information about a specific item in the TOC
        /// </summary>
        private void RequestTocElement(ushort index)
        {

            _log.Debug($"Requesting index {index} on port {_port}");

            byte[] content;            
            if (_useV2)
            {
                content = new byte[] { (byte)TocCommand.CMD_TOC_ITEM_V2,
                    (byte)(index & 0x0ff), (byte) ((index >> 8) & 0x0ff)};                
            }
            else
            {
                content = new byte[] { (byte)TocCommand.CMD_TOC_ELEMENT, (byte)(index & 0xff) };
            }

            var msg = new CrtpMessage(_port, TOC_CHANNEL,
                content);
            _communicator.SendMessageExcpectAnswer(msg, msg.Data.Take(content.Length).ToArray());
        }

    }
}
