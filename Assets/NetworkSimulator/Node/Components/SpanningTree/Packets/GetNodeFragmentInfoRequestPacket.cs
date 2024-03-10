using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    public class GetNodeFragmentInfoRequestPacket : AbstractNetworkPacket, IRespondable
    {
        public string senderAdress { get; set ; }

        public INetworkPacket packet => this;

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new GetNodeFragmentInfoResponsePacket();
        }
    }

    public class GetNodeFragmentInfoResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set ; }

        public INetworkPacket packet => this;

        public int requestPing { get; set; }
        public GraphFragmentData graphFragmentData { get; set; }
    }
}
