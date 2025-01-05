using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    public class MinimumOutgoingEdgePacket : AbstractBroadcastablePacket
    {
        public MinimumOutgoingEdge MinimumOutgoingEdge { get; set; }

        public MinimumOutgoingEdgePacket() { }
        public MinimumOutgoingEdgePacket(MinimumOutgoingEdge minimumOutgoingEdge)
        {
            MinimumOutgoingEdge = minimumOutgoingEdge;
        }

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (INetworkPacket)(received as MinimumOutgoingEdgePacket).MemberwiseClone();
        }
    }
}
