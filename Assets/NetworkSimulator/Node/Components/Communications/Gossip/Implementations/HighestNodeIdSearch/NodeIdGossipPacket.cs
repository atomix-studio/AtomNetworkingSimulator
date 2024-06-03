using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.Gossip
{
    public class NodeIdGossipPacket : AbstractBroadcastablePacket, IGossipPacket
    {
        public PeerInfo HighestIdKnownPeer;

        public DateTime gossipStartedTime { get; set; }
        public long gossipId { get ; set; }
        public int gossipGeneration { get; set; }

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (INetworkPacket)((NodeIdGossipPacket)received).MemberwiseClone();
        }
    }
}
