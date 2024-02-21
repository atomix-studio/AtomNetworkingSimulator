using Atom.CommunicationSystem;

namespace Atom.ClusterConnectionService
{
    public class ClusterConnectionRequestPacket : AbstractNetworkPacket, IRespondable
    {
        public string senderAdress { get ; set ; }

        public ClusterConnectionRequestPacket()
        {
        }

        public INetworkPacket GetResponsePacket(INetworkPacket answerPacket)
        {
            return new ClusterConnectionRequestResponsePacket() { callerPacketUniqueId = answerPacket.packetUniqueId };
        }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new ClusterConnectionRequestResponsePacket();
        }
    }

    public class ClusterConnectionRequestResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set ; }
    }
}
