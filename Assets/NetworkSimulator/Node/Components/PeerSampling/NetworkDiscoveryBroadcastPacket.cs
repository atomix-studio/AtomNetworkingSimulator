using Atom.Components.PeerCounting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public class NetworkDiscoveryBroadcastPacket : AbstractNetworkPacket, IRespondable, IBroadcastable
    {
        public string senderAdress { get ; set ; }
        public string broadcasterID { get; set ; }
        public string broadcastID { get; set; }

        public INetworkPacket packet => this;


        public NetworkDiscoveryBroadcastPacket() { }

        public NetworkDiscoveryBroadcastPacket(short packetIdentifier, string senderID, DateTime sentTime, string broadcastID, string broadcasterID)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
        }

        public NetworkDiscoveryBroadcastPacket(NetworkDiscoveryBroadcastPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID)
        {

        }

        public INetworkPacket GetForwardablePacket(INetworkPacket received)
        {
            return new NetworkDiscoveryBroadcastPacket(received as NetworkDiscoveryBroadcastPacket);
        }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new NetworkDiscoveryBroadcastResponsePacket();
        }
    }

    public class NetworkDiscoveryBroadcastResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set; }
    }
}
