using Atom.CommunicationSystem;

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
            return new ClusterConnectionRequestResponsePacket() { callerPacketUniqueId = answerPacket.packet.packetUniqueId };
        }
    }

    public class ClusterConnectionRequestResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set ; }
        public INetworkPacket packet => this;
    }
}
