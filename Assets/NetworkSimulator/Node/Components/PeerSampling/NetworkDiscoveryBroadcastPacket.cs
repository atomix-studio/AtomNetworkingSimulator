using Atom.Components.PeerCounting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public class NetworkDiscoveryBroadcastPacket : AbstractNetworkPacket, IRespondable, IBroadcastablePacket
    {
        public string senderAdress { get; set; }
        public string broadcasterID { get; set; }
        public string broadcastID { get; set; }

        public INetworkPacket packet => this;

        // not all broadcast messages needs to hold the adress of the broadcaster
        // for discovery over network we need that data because a receiver will have to ping the broadcaster to determine if a connection is profitable
        public string broadcasterAdress { get; set; }

        public NetworkDiscoveryBroadcastPacket(string broadcasterAdress)
        {
            this.broadcasterAdress = broadcasterAdress;
        }

        public NetworkDiscoveryBroadcastPacket(short packetIdentifier, string senderID, DateTime sentTime, string broadcastID, string broadcasterID, string broadcasterAdress)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.broadcasterAdress = broadcasterAdress;
        }

        public NetworkDiscoveryBroadcastPacket(NetworkDiscoveryBroadcastPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID, subscriptionPacket.broadcasterAdress)
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
        public string listennerAdress { get; set; }
        public string listennerID { get; set; }

        public INetworkPacket packet => this;
    }
}
