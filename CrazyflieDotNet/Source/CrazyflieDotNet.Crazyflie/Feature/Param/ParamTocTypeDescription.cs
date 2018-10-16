using CrazyflieDotNet.Crazyflie.Feature.Common;
using System;


namespace CrazyflieDotNet.Crazyflie.Feature.Param
{
    internal class ParamTocTypeDescription : TocTypeDescription
    {
        public Func<object, byte[]> EncodeFunc { get; }

        public ParamTocTypeDescription(string name, Func<byte[], object> decodeFunc, Func<object, byte[]> encodeFunc, byte size) : base(name, decodeFunc, size)
        {
            EncodeFunc = encodeFunc;
        }


    }
}
