using System;

namespace CrazyflieDotNet.Crazyflie.Feature.Common
{
    /// <summary>
    /// Class which helps to decode toc elements
    /// </summary>
    internal class TocTypeDescription
    {

        public Func<byte[], object> DecodeFunc { get; }
        public string Name { get; }
        public byte Size { get; }

        public TocTypeDescription(string name, Func<byte[], object> decodeFunc, byte size)
        {
            DecodeFunc = decodeFunc;
            Name = name;
            Size = size;

        }
    }

}
