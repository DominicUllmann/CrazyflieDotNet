using CrazyflieDotNet.Crazyradio.Driver;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrazyflieDotNet.Crazyradio
{
    public class CrazyflieUri : IEquatable<CrazyflieUri>
    {
        public int DeviceId { get; }
        public CrazyflieId Id { get; }

        public CrazyflieUri(int deviceId, CrazyflieId id)
        {
            DeviceId = deviceId;
            Id = id;
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
