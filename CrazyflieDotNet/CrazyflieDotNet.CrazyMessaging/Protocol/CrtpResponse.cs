using System;

namespace CrazyflieDotNet.CrazyMessaging.Protocol
{

    /// <summary>
    /// the response message received as a result of sending.
    /// It consist of an ack status and a CrtpMessage
    /// 
    /// </summary>
    public class CrtpResponse
    {
        private static readonly byte[] _emptyContent = new byte[0];

        private byte _ackStatus;

        public CrtpResponse(byte[] result)
        {
            if (result.Length > 0)
            {
                _ackStatus = result[0];

            }
            byte header = 0;
            byte[] content = _emptyContent;
            if (result.Length > 1)
            {                
                header = result[1];
                HasContent = true;
            }

            if (result.Length > 2)
            {            
                content = new byte[result.Length - 2];
                Array.Copy(result, 2, content, 0, content.Length);
            }
            
            Content = new CrtpMessage(header, content);
        }

        public bool Ack
        {
            get
            {
                return (_ackStatus & 0x01) != 0;
            }
        }

        public bool PowerDet
        {
            get
            {
                return (_ackStatus & 0x02) != 0;
            }
        }

        public byte Retry
        {
            get
            {
                return (byte)(_ackStatus >> 4);
            }
        }

        public bool HasContent { get; }

        public CrtpMessage Content { get; }

    }
}
