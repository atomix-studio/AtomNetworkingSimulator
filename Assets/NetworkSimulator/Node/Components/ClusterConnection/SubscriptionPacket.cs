using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public class SubscriptionPacket : AbstractNetworkPacket, IBroadcastable, IRespondable
    {       
        public string broadcasterID { get; set; }
        public string broadcastID { get; set; }

        // adress of the broadcaster, to allow peers to actually respond/connect to the broadcaster
        public INetworkPacket packet => this;

        public string senderAdress { get ; set ; }

        public SubscriptionPacket() { }

        public SubscriptionPacket(short packetIdentifier, string senderID, DateTime sentTime, string broadcasterID, string broadcastID, string senderAdress)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.senderAdress = senderAdress;
        }

        public SubscriptionPacket(SubscriptionPacket subscriptionPacket) : this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcasterID, subscriptionPacket.broadcastID, subscriptionPacket.senderAdress)
        {

        }

        public INetworkPacket GetForwardablePacket(INetworkPacket received)
        {
            var copy = new SubscriptionPacket(received as SubscriptionPacket);
            return copy;
        }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new SubscriptionResponsePacket();
        }
    }

    public class SubscriptionResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set ; }

        /// <summary>
        /// A collection of peer infos provided by the sender of this response / a starting point for the node entering in the network
        /// </summary>
        public List<PeerInfo> potentialPeerInfos { get; set; }
    }
}
