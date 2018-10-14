namespace CrazyflieDotNet.Crazyflie.Feature.Log
{

    /// <summary>
    /// A logging variable
    /// </summary>
    public class LogVariable
    {
        public enum LogType : byte {
            TOC_TYPE = 0,
            MEM_TYPE = 1
        }

        private readonly byte _type;

        public LogVariable(string name, string fetchAs)
        {
            Name = name;
            FetchAsId = LogTocElement.GetIdFromCString(fetchAs);
            StoredAsId = FetchAsId;
            _type = (byte)LogType.TOC_TYPE;
        }

        public LogVariable(string name, string fetchAs, string storedAs, uint address) : this(name, fetchAs)
        {
            StoredAsId = LogTocElement.GetIdFromCString(storedAs);
            Address = address;
            _type = (byte)LogType.MEM_TYPE;
        }

        public bool IsTocVariable
        {
            get
            {
                return _type == (byte)LogType.TOC_TYPE;
            }
        }

        public uint Address { get; internal set; }
        public string Name { get; internal set; }
        public byte FetchAsId { get; }
        public byte StoredAsId { get; }

        public override string ToString()
        {
            return $"LogVariable {Name} Store {LogTocElement.GetCStringFromId(StoredAsId)} Fetch {LogTocElement.GetCStringFromId(FetchAsId)}";
        }

        /// <summary>
        /// Return what the variable is stored as and fetched as
        /// </summary>
        internal byte GetStorageAndFetchByte()
        {
            return (byte)(FetchAsId | (StoredAsId << 4));
        }
    }
}
