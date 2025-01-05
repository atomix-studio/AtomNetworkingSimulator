using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.HierarchicalTree
{
    internal class SetColorDowncastPacket : AbstractNetworkPacket, IDowncastablePacket
    {
        public long broadcasterID { get; set; }
        public long broadcastID { get; set; }
        public Color newColor { get; set; }

        public SetColorDowncastPacket() {

            Debug.Log("downcast");
        }

        public SetColorDowncastPacket(short packetIdentifier, long senderID, DateTime sentTime, long broadcastID, long broadcasterID, Color newColor)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.newColor = newColor;
            Debug.Log("downcast");
        }

        public SetColorDowncastPacket(SetColorDowncastPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID, subscriptionPacket.newColor)
        {
            Debug.Log("downcast");
        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new SetColorDowncastPacket(received as SetColorDowncastPacket);
        }
    }

    internal class SetColorUpcastPacket : AbstractNetworkPacket, IUpcastablePacket
    {
        public long broadcasterID { get; set; }
        public long broadcastID { get; set; }
        public Color newColor { get; set; }

        public SetColorUpcastPacket() { }

        public SetColorUpcastPacket(short packetIdentifier, long senderID, DateTime sentTime, long broadcastID, long broadcasterID, Color newColor)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.newColor = newColor;
        }

        public SetColorUpcastPacket(SetColorUpcastPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID, subscriptionPacket.newColor)
        {

        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new SetColorUpcastPacket(received as SetColorUpcastPacket);
        }
    }
}
