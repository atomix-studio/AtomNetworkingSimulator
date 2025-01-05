using Atom.Components.PeerCounting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public class NetworkDiscoveryBroadcastPacket : AbstractBroadcastablePacket
    {
        public INetworkPacket packet => this;

        // not all broadcast messages needs to hold the adress of the broadcaster
        // for discovery over network we need that data because a receiver will have to ping the broadcaster to determine if a connection is profitable
        public string broadcasterAdress { get; set; }

        public NetworkDiscoveryBroadcastPacket(string broadcasterAdress)
        {
            this.broadcasterAdress = broadcasterAdress;
        }

        public NetworkDiscoveryBroadcastPacket(short packetIdentifier, long senderID, DateTime sentTime, long broadcastID, long broadcasterID, string broadcasterAdress)
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

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new NetworkDiscoveryBroadcastPacket(received as NetworkDiscoveryBroadcastPacket);
        }
    }

    public class NetworkDiscoveryPotentialConnectionNotificationPacket : AbstractNetworkPacket
    {     
        public string listennerAdress { get; set; }
        public string listennerID { get; set; }

        public INetworkPacket packet => this;
    }
}
