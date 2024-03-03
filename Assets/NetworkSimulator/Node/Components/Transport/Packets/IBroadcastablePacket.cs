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
        public string broadcasterID { get; set; }   

        // identifier of the broadcaster
        public string broadcastID { get; set; }
    }
}
