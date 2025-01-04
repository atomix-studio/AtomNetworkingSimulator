using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.HierarchicalTree
{
    internal class UpdateRankPacket : AbstractNetworkPacket, IDowncastablePacket
    {
        public long broadcasterID { get; set; }
        public long broadcastID { get; set; }      
        public int newRank { get; set; }

        public UpdateRankPacket() { }

        public UpdateRankPacket(short packetIdentifier, long senderID, DateTime sentTime, long broadcastID, long broadcasterID, int newRank)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.newRank = newRank;
        }

        public UpdateRankPacket(UpdateRankPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID, subscriptionPacket.newRank)
        {

        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new UpdateRankPacket(received as UpdateRankPacket);
        }
    }
}
