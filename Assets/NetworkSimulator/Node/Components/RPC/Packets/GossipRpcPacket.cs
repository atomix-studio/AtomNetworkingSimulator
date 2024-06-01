using Atom.Components.Gossip;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.RpcSystem
{
    public class GossipRpcPacket : BroadcastedRpcPacket, IGossipPacket
    {
        public DateTime gossipStartedTime { get ; set; }
        public long gossipId { get; set; }
        public int gossipGeneration { get; set; }
    }
}
