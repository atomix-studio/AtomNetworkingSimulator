using Atom.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public class HandshakePacket : AbstractNetworkPacket, IRespondable<HandshakePacket, HandshakeResponsePacket>
    {       
        public string senderAdress { get; set; }
        public HandshakePacket packet => this;

        public IResponse<HandshakeResponsePacket> GetResponsePacket(IRespondable<HandshakePacket, HandshakeResponsePacket> answerPacket)
        {
            return (IResponse<HandshakeResponsePacket>)new HandshakeResponsePacket();
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
        [SerializerIgnore] public int requestPing { get; set; }
    }
}
