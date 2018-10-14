namespace CrazyflieDotNet.CrazyMessaging.Protocol
{

    /// <summary>
    ///     A CrtpMessage with one byte header and a data array.
    /// 
    ///     Header Format (1 byte):
    ///     8  7  6  5  4  3  2  1
    ///     [  Port#  ][Res.][Ch.]
    ///     Res. = reserved for transfer layer.
    ///     Ch. = Channel
    /// </summary>
    /// <remarks>
    /// Known Ports are defined in <see cref="CrtpPort"/>
    /// </remarks>
    public class CrtpMessage
    {

        public byte Header { get; }
        public byte[] Data { get; }
        public byte Port { get; }
        public byte Channel { get; }

        public CrtpMessage(byte header, byte[] data)
        {
            Header = header;
            Data = data;
            Port = (byte)((header & 0xF0) >> 4);
            Channel = (byte)(header & 0x03);        
        }

        public CrtpMessage(byte port, byte channel, byte[] data) :
            this(CalculateHeader(port, channel), data)
        {
            Port = port;
            Channel = channel;
        }

        private static byte CalculateHeader(byte port, byte channel)
        {
            return (byte)(((port & 0x0f) << 4 | 3 << 2 |
                            (channel & 0x03)));
        }

        /// <summary>
        ///  the complete message consisting of header and Data.
        /// </summary>
        public byte[] Message 
        {
            get
            {
                var result = new byte[1 + Data.Length];
                result[0] = Header;
                Data.CopyTo(result, 1);
                return result;
            }
        }

    }
}
