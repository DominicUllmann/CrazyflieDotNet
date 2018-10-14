using System.Collections.Generic;

namespace CrazyflieDotNet.Crazyflie.Feature.Common
{
    internal class TocCache<T>         
        where T : ITocElement, new() {

        private IDictionary<uint, Toc<T>> _cached = new Dictionary<uint, Toc<T>>();


        public Toc<T> GetByCrc(uint crc)
        {
            Toc<T> resultToc;
            _cached.TryGetValue(crc, out resultToc);
            return resultToc;
        }

        public void AddToc(uint crc, Toc<T> toc)
        {
            _cached[crc] = toc;
        }

    }
}
