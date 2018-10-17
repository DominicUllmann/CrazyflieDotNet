using CrazyflieDotNet.Crazyflie.Feature.Common;
using CrazyflieDotNet.Crazyflie.Feature.Param;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrazyflieDotNet.Crazyflie.Feature.Parameter
{
    /// <summary>
    /// An element in the Param TOC
    /// </summary>
    public class ParamTocElement : ITocElement
    {

        private static readonly IDictionary<byte, ParamTocTypeDescription> _paramTypes =
             new Dictionary<byte, ParamTocTypeDescription>()
             {
                { 0x08, new ParamTocTypeDescription("uint8_t", x => x[0], x => new byte[] { (byte)x }, 1) },
                { 0x09, new ParamTocTypeDescription("uint16_t", x => BitConverter.ToUInt16(x, 0), x => BitConverter.GetBytes((ushort)x), 2) },
                { 0x0A, new ParamTocTypeDescription("uint32_t", x => BitConverter.ToUInt32(x, 0), x => BitConverter.GetBytes((uint)x), 4) },
                { 0x0B, new ParamTocTypeDescription("uint64_t", x => BitConverter.ToUInt64(x, 0), x => BitConverter.GetBytes((ulong)x), 8) },
                { 0x00, new ParamTocTypeDescription("int8_t", x => (sbyte)x[0],  x => new byte[] { Convert.ToByte(x) }, 1) },
                { 0x01, new ParamTocTypeDescription("int16_t", x => BitConverter.ToInt16(x, 0), x => BitConverter.GetBytes((short)x), 2) },
                { 0x02, new ParamTocTypeDescription("int32_t", x => BitConverter.ToInt32(x, 0), x => BitConverter.GetBytes((int)x), 4) },
                { 0x03, new ParamTocTypeDescription("int64_t", x => BitConverter.ToInt64(x, 0), x => BitConverter.GetBytes((long)x), 8) },
                { 0x05, new ParamTocTypeDescription("FP16", x => BitConverter.ToUInt16(x, 0), x => BitConverter.GetBytes((ushort)x), 2) },
                { 0x06, new ParamTocTypeDescription("float", x => BitConverter.ToSingle(x, 0), x => BitConverter.GetBytes((float)x), 4) },
                { 0x07, new ParamTocTypeDescription("double", x => BitConverter.ToDouble(x, 0),  x => BitConverter.GetBytes((double)x), 8) }                
             };


        public enum AccessLevel
        {
            Readonly,
            Readwrite,
        }

        [JsonProperty]
        public AccessLevel Access
        {
            get; private set;
        }

        [JsonProperty]
        public string CType
        {
            get; private set;
        }

        [JsonProperty]
        public ushort Identifier
        {
            get; private set;
        }

        [JsonProperty]
        public string Name
        {
            get; private set;
        }

        [JsonProperty]
        public string Group
        {
            get; private set;
        }

        private static Encoding _encoder = Encoding.GetEncoding("ISO-8859-1");

        public ParamTocElement()
        {
            Identifier = 0;
        }

        public ParamTocElement(ushort identifier, byte[] data)
        {
            InitializeFrom(identifier, data);
        }

        /// <summary>
        /// "Return the C-storage name given the variable type id"
        /// </summary>
        public static string GetCStringFromId(byte id)
        {
            if (!_paramTypes.ContainsKey(id))
            {
                throw new ArgumentException("unknown param type id: " + id, nameof(id));
            }
            return _paramTypes[id].Name;
        }

        /// <summary>
        /// Return variable type id given the C-storage name
        /// </summary>
        public static byte GetIdFromCString(string name)
        {
            foreach (var element in _paramTypes)
            {
                if (element.Value.Name == name)
                {
                    return element.Key;
                }
            }
            throw new ArgumentException("unkonwn name" + name, nameof(name));
        }

        /// <summary>
        /// TocElement creator. Data is the binary payload of the element.
        /// </summary>
        public void InitializeFrom(ushort identifier, byte[] data)
        {
            Identifier = identifier;
            var groupEncoded = data.Skip(1).TakeWhile(x => x != 0).ToArray();
            var nameEncoded = data.Skip(1 + groupEncoded.Length + 1)
                .TakeWhile(x => x != 0)
                .ToArray();

            Group = _encoder.GetString(groupEncoded);
            Name = _encoder.GetString(nameEncoded);
            CType = GetCStringFromId((byte)(data[0] & 0x0F));

            var metadata = data[0];

            if ((metadata & 0x40) != 0) {
                Access = AccessLevel.Readonly;
            } else
            {
                Access = AccessLevel.Readwrite;
            }
        }

        /// <summary>
        /// Unpack byte array according to id.
        /// </summary>        
        public static object Unpack(byte id, byte[] data)
        {
            return _paramTypes[id].DecodeFunc(data);
        }

        /// <summary>
        /// pack to byte array according to id.
        /// </summary>        
        public static byte[] Pack(byte id, object data)
        {
            return _paramTypes[id].EncodeFunc(data);
        }
    }
}
