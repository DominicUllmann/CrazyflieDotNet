using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrazyflieDotNet.Crazyflie.Feature.Log
{

    /// <summary>
    /// An element in the Log TOC.
    /// </summary>
    public class LogTocElement
    {                

        private class LogTocTypeDescription
        {
            public Func<byte[], object> DecodeFunc { get; }
            public string Name { get; }
            public byte Size { get; }            

            public LogTocTypeDescription(string name, Func<byte[], object> decodeFunc, byte size)
            {
                DecodeFunc = decodeFunc;
                Name = name;
                Size = size;

            }
        }

        private static readonly IDictionary<byte, LogTocTypeDescription> _logTypes = 
            new Dictionary<byte, LogTocTypeDescription>()
            {
                { 0x01, new LogTocTypeDescription("uint8_t", x => x[0], 1) },
                { 0x02, new LogTocTypeDescription("uint16_t", x => BitConverter.ToUInt16(x, 0), 2) },
                { 0x03, new LogTocTypeDescription("uint32_t", x => BitConverter.ToUInt32(x, 0), 4) },
                { 0x04, new LogTocTypeDescription("int8_t", x => (sbyte)x[0], 1) },
                { 0x05, new LogTocTypeDescription("int16_t", x => BitConverter.ToInt16(x, 0), 2) },
                { 0x06, new LogTocTypeDescription("int32_t", x => BitConverter.ToInt32(x, 0), 4) },
                { 0x08, new LogTocTypeDescription("FP16", x => BitConverter.ToUInt16(x, 0), 2) },
                { 0x07, new LogTocTypeDescription("float", x => BitConverter.ToSingle(x, 0), 4) },
            };


        public byte Access
        {
            get;
        }

        public string Name
        {
            get;
        }

        public string Group
        {
            get;
        }

        public string CType
        {
            get;
        }

        public ushort Identifier
        {
            get;
        }

        private static Encoding _encoder = Encoding.GetEncoding("ISO-8859-1");

        /// <summary>
        /// Data is the binary payload of the element.
        /// </summary>
        public LogTocElement(ushort identifier, byte[] data)
        {
            Identifier = identifier;

            var groupEncoded = data.Skip(1).TakeWhile(x => x != 0).ToArray();
            var nameEncoded = data.Skip(1 + groupEncoded.Length + 1)
                .TakeWhile(x => x != 0)
                .ToArray();


            Group = _encoder.GetString(groupEncoded);
            Name = _encoder.GetString(nameEncoded);
            CType = GetCStringFromId(data[0]);
            Access = (byte)(data[0] & 0x10);
        }

        /// <summary>
        /// Return variable type id given the C-storage name
        /// </summary>
        public static byte GetIdFromCString(string name)
        {
            foreach (var element in _logTypes)
            {
                if (element.Value.Name == name)
                {
                    return element.Key;
                }
            }
            throw new ArgumentException("unkonwn name" + name, nameof(name));
        }

        /// <summary>
        /// "Return the C-storage name given the variable type id"
        /// </summary>
        public static string GetCStringFromId(byte id)
        {
            if (!_logTypes.ContainsKey(id))
            {
                throw new ArgumentException("unknown log type id: " + id, nameof(id));
            }
            return _logTypes[id].Name;
        }

        /// <summary>
        /// Return the size in bytes given the variable type id
        /// </summary>
        public static byte GetSizeFromId(byte id)
        {
            if (!_logTypes.ContainsKey(id))
            {
                throw new ArgumentException("unknown log type id: " + id, nameof(id));
            }
            return _logTypes[id].Size;
        }

        /// <summary>
        /// Unpack byte array according to id.
        /// </summary>        
        public static object Unpack(byte id, byte[] data)
        {
            return _logTypes[id].DecodeFunc(data);
        }

    }
}
