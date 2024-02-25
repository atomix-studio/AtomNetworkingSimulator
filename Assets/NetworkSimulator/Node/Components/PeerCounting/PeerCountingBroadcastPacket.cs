using Atom.CommunicationSystem;
using Atom.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.PeerCounting
{
    public class PeerCountingBroadcastPacket : AbstractNetworkPacket, IBroadcastablePacket, IRespondable
    {        
        public string broadcasterID { get; set; }
        public string broadcastID { get; set; }

        public INetworkPacket packet => this;

        public string senderAdress { get ; set ; }

        public PeerCountingBroadcastPacket() { }

        public PeerCountingBroadcastPacket(short packetIdentifier, string senderID, DateTime sentTime, string broadcastID, string broadcasterID)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
        }

        public PeerCountingBroadcastPacket(PeerCountingBroadcastPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID)
        {

        }

        public INetworkPacket GetForwardablePacket(INetworkPacket received)
        {
            return new PeerCountingBroadcastPacket(received as PeerCountingBroadcastPacket);
        }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new PeerCountingBroadcastResponsePacket();
        }
    }

    public class PeerCountingBroadcastResponsePacket : AbstractNetworkPacket, IResponse
    {        
        public long callerPacketUniqueId { get; set; }

        public INetworkPacket packet => this;
        [SerializerIgnore] public int requestPing { get; set; }

        public PeerCountingBroadcastResponsePacket() { }
    }
}
