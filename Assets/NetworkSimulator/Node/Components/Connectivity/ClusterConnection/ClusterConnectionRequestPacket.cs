using Atom.CommunicationSystem;
using Atom.Serialization;
using System.Collections.Generic;

namespace Atom.ClusterConnectionService
{
    public class ClusterConnectionRequestPacket : AbstractNetworkPacket, IRespondable
    {
        public string senderAdress { get ; set ; }

        public INetworkPacket packet => this;

        public ClusterConnectionRequestPacket()
        {
        }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new ClusterConnectionRequestResponsePacket();
        }
    }

    public class ClusterConnectionRequestResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set ; }
        public INetworkPacket packet => this;
        public List<PeerInfo> potentialPeerInfos { get; set; }
        [SerializerIgnore] public int requestPing { get; set; }
    }
}
