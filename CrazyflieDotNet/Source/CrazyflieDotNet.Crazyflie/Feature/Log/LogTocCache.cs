using System.Collections.Generic;

namespace CrazyflieDotNet.Crazyflie.Feature.Log
{
    internal class LogTocCache
    {

        private IDictionary<uint, LogToc> _cached = new Dictionary<uint, LogToc>();


        internal LogToc GetByCrc(uint crc)
        {
            LogToc resultToc;
            _cached.TryGetValue(crc, out resultToc);
            return resultToc;
        }

        internal void AddToc(uint crc, LogToc toc)
        {
            _cached[crc] = toc;
        }

    }
}
