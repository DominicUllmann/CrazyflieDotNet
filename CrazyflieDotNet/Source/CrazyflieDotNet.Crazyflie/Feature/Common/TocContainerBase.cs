﻿using CrazyflieDotNet.CrazyMessaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrazyflieDotNet.Crazyflie.Feature.Common
{
    internal abstract class TocContainerBase<T> : ITocContainer<T> where T : ITocElement, new()
    {
        protected bool _useV2Protocol;
        protected ICrtpCommunicator _communicator;

        private TocCache<T> _tocCache = new TocCache<T>();

        public Toc<T> CurrentToc { get; private set; } = null;
        private ManualResetEvent _loadTocDone = new ManualResetEvent(false);
        private TocFetcher<T> _tocFetcher;

        protected TocContainerBase(ICrtpCommunicator communicator, bool useV2Protocol, byte port)
        {
            _useV2Protocol = useV2Protocol;
            _communicator = communicator;            

            _tocFetcher = new TocFetcher<T>(_communicator, _tocCache,
                port, _useV2Protocol);
            _tocFetcher.TocReceived += TocFetcher_TocReceived;
        }

        public Task<Toc<T>> RefreshToc()
        {
            CurrentToc = null;
            _loadTocDone.Reset();
            var task = new Task<Toc<T>>(() => LoadToc());
            task.Start();
            return task;
        }

        private Toc<T> LoadToc()
        {
            StartLoadToc();

            if (!_loadTocDone.WaitOne(40000))
            {
                throw new ApplicationException($"failed to download toc for {typeof(T).Name} (timeout)");
            }
            return CurrentToc;
        }

        /// <summary>
        /// Initialize the loading of the toc by either sending a prepare message
        /// or directly loading the toc from the fetcher.
        /// </summary>
        protected abstract void StartLoadToc();
        
        protected void FetchTocFromTocFetcher()
        {
            CurrentToc = _tocFetcher.Start();
        }

        private void TocFetcher_TocReceived(object sender, TocFetchedEventArgs e)
        {
            // signal the waiting task that toc has been received.
            _loadTocDone.Set();
        }

    }
}
