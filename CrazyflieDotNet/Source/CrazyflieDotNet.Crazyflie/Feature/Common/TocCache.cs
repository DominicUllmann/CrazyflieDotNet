using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CrazyflieDotNet.Crazyflie.Feature.Common
{
    internal class TocCache<T>         
        where T : ITocElement, new() {

        private IDictionary<uint, Toc<T>> _cached = new Dictionary<uint, Toc<T>>();
        private DirectoryInfo _cacheDirectory;

        public TocCache(DirectoryInfo cacheDiretory)
        {                        
            if (!cacheDiretory.Exists)
            {
                cacheDiretory.Create();
            }

            _cacheDirectory = cacheDiretory;
        }

        public Toc<T> GetByCrc(uint crc)
        {
            lock (_cached)
            {
                Toc<T> resultToc;
                if (!_cached.TryGetValue(crc, out resultToc))
                {
                    var cachedFile = GetFromFileCache(crc);
                    if (cachedFile != null)
                    {
                        var fromFile = Toc<T>.DeserializeFromFile(cachedFile);
                        _cached[crc] = fromFile;
                        resultToc = fromFile;
                    }
                }
                return resultToc;
            }
        }

        private string GetFileName(uint crc)
        {
            return $"{crc.ToString("X")}.json";
        }

        private FileInfo GetFromFileCache(uint crc)
        {
            var files = _cacheDirectory.GetFiles(GetFileName(crc));
            if (files != null && files.Any())
            {
                return files[0];
            }
            return null;
        }

        public void AddToc(uint crc, Toc<T> toc)
        {
            lock (_cached)
            {
                _cached[crc] = toc;
                if (GetFromFileCache(crc) == null)
                {
                    toc.SerializeToFile(new FileInfo(Path.Combine(_cacheDirectory.FullName, GetFileName(crc))));
                }
            }
        }

    }
}
