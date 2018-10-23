using System;
using System.Collections.Generic;
using System.Text;

namespace CrazyflieDotNet.Crazyradio.Parallel
{
    internal class CrazyRadioCommunicationTicket : ICrazyRadioCommunicationTicket
    {
        private CrazyRadioCommunicationLock _communicationLock;

        public CrazyRadioCommunicationTicket(CrazyRadioCommunicationLock radioLock)
        {

        }

        public void Dispose()
        {
            
        }
    }
}
