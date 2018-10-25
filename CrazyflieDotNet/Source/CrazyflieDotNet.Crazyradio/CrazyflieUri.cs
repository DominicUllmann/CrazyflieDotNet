using CrazyflieDotNet.Crazyradio.Driver;
using System;

namespace CrazyflieDotNet.Crazyradio
{
    public class CrazyflieUri : IEquatable<CrazyflieUri>
    {
        public int DeviceId { get; }
        public CrazyflieId Id { get; }

        /// <summary>
        /// Constructor which allows to specify the crazyradio device number to use (zero based).
        /// </summary>
        public CrazyflieUri(int deviceId, CrazyflieId id)
        {
            DeviceId = deviceId;
            Id = id;
        }

        /// <summary>
        /// Constructor to use in case you have only one crazyradio installed.
        /// </summary>
        public CrazyflieUri(CrazyflieId id) : this(0, id)
        {
        }

        public bool Equals(CrazyflieUri other)
        {
            if (other == null)
            {
                return false;
            }
            return DeviceId == other.DeviceId && Object.Equals(other.Id, Id);
        }

        public override bool Equals(object other)
        {
            return Equals((CrazyflieUri)other);
        }

        public override int GetHashCode()
        {
            // TODO
            return (Id != null ? Id.GetHashCode() : 0)
                ^ DeviceId.GetHashCode();
        }
    }
}
