using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public class HandshakePacket : AbstractNetworkPacket, IRespondable
    {       
        public string senderAdress { get; set; }
        public INetworkPacket packet => this;

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new HandshakeResponsePacket();
        }
    }

    public class HandshakeResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set; }

        /// <summary>
        /// number of callers the node have
        /// </summary>
        public byte networkInfoCallersCount { get; set; }

        /// <summary>
        /// number of listenners the node have
        /// </summary>
        public byte networkInfoListennersCount { get; set; }

        public INetworkPacket packet => this;
    }
}
