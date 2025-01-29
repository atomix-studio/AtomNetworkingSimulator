using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.HierarchicalTree
{
    /// <summary>
    /// Close any connection with a peer in the tree
    /// </summary>
    internal class DisconnectionNotificationPacket : AbstractNetworkPacket
    {
    }

    /// <summary>
    /// Disbands all subgraph
    /// </summary>
    internal class DisconnectionDowncastPacket : AbstractNetworkPacket, IDowncastablePacket
    {
        public long broadcasterID { get; set; }
        public long broadcastID { get; set; }

        public DisconnectionDowncastPacket()
        {

        }

        public DisconnectionDowncastPacket(short packetIdentifier, long senderID, DateTime sentTime, long broadcastID, long broadcasterID)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
        }

        public DisconnectionDowncastPacket(DisconnectionDowncastPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID)
        {
        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new DisconnectionDowncastPacket(received as DisconnectionDowncastPacket);
        }
    }

}
