using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.HierarchicalTree
{
    internal class BroadcastColorPacket : AbstractNetworkPacket, IBroadcastablePacket
    {
        public long broadcasterID { get; set; }
        public long broadcastID { get; set; }
        public Color newColor { get; set; }

        public BroadcastColorPacket() { }

        public BroadcastColorPacket(short packetIdentifier, long senderID, DateTime sentTime, long broadcastID, long broadcasterID, Color newColor)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.newColor = newColor;
        }

        public BroadcastColorPacket(BroadcastColorPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID, subscriptionPacket.newColor)
        {

        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new BroadcastColorPacket(received as BroadcastColorPacket);
        }
    }
}
