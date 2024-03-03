using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    public class SpanningTreeCreationBroadcastPacket : AbstractBroadcastablePacket
    {
        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (SpanningTreeCreationBroadcastPacket)(received as SpanningTreeCreationBroadcastPacket).MemberwiseClone();
        }
    }
}
