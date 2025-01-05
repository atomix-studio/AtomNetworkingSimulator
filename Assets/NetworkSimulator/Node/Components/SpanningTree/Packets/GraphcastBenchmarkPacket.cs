using Atom.CommunicationSystem;
using Atom.Components.GraphNetwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    internal class GraphcastBenchmarkPacket : AbstractBroadcastablePacket
    {
        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (INetworkPacket)((GraphcastBenchmarkPacket)received).MemberwiseClone();
        }
    }
}
