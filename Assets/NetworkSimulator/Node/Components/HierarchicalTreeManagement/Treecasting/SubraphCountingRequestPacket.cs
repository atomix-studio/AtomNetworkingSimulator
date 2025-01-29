using Atom.CommunicationSystem;
using Atom.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.HierarchicalTree
{
    internal class SubraphCountingRequestPacket : AbstractNetworkPacket, IRespondable
    {
        public string senderAdress { get; set; }
        public INetworkPacket packet => this;
        public int depth { get; set; }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new SubgraphCountingResponsePacket();
        }
    }

    internal class SubgraphCountingResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set; }
        public INetworkPacket packet => this;
        [SerializerIgnore] public int requestPing { get; set; }

        public int childrenCount { get; set; }
        public int maxDepth { get; set; }
    }
}
