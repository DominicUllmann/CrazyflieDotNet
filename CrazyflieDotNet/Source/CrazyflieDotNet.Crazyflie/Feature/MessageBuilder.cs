using CrazyflieDotNet.CrazyMessaging.Protocol;
using System;
using System.IO;

namespace CrazyflieDotNet.Crazyflie.Feature
{
    internal class MessageBuilder
    {

        private readonly MemoryStream _data;
        private readonly byte _channel;
        private readonly byte _port;

        internal MessageBuilder(byte port, byte channel = (byte)CrtpChannel.Channel0)
        {
            _data = new MemoryStream();
            _channel = channel;
            _port = port;
        }

        public CrtpMessage Build()
        {            
            return new CrtpMessage(_port, _channel, _data.ToArray());
        }

        public void Add(byte argument)
        {            
            _data.WriteByte(argument);
        }

        public void Add(float argument)
        {
            var encoded = BitConverter.GetBytes(argument);
            Add(encoded);
        }

        public void Add(ushort argument)
        {
            var encoded = BitConverter.GetBytes(argument);
            Add(encoded);
        }

        public void Add(byte[] encoded)
        {
            _data.Write(encoded, 0, encoded.Length);
        }

        internal static byte GetPayLoadLength(byte argument)
        {
            return 1;
        }

        internal static byte GetPayLoadLength(float argument)
        {
            return sizeof(float);
        }

        internal static byte GetPayLoadLength(ushort argument)
        {
            return sizeof(ushort);
        }
    }
}
