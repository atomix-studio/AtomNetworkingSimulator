using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public interface IBroadcastablePacket  : INetworkPacket, IClonablePacket
    {
        public long broadcasterID { get; set; }   

        // identifier of the broadcaster
        public long broadcastID { get; set; }
    }
}
