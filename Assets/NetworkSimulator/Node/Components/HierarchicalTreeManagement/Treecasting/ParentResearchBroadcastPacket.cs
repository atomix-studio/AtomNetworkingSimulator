using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.HierarchicalTree
{
    public class ParentResearchBroadcastPacket : AbstractNetworkPacket, IBroadcastablePacket
    {
        public long broadcasterID { get; set; }
        public long broadcastID { get; set; }
        public Vector3 senderPosition { get; set; }
        public int senderRank { get; set; }
        public int cyclesDistance { get; set; }
        public int maxCycleDistance { get; set; }
        public string senderAddress { get; set; }
        public int senderRound { get; set; }

        public ParentResearchBroadcastPacket() { }

        public ParentResearchBroadcastPacket(short packetIdentifier, long senderID, DateTime sentTime, long broadcastID, long broadcasterID, string senderAddress, Vector3 senderPosition, int senderRank, int senderRound, int currentCycleDistance, int maxCycleDistance)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.senderRank = senderRank;
            this.senderAddress = senderAddress;
            this.senderRound = senderRound;
            this.cyclesDistance = currentCycleDistance;
            this.senderPosition = senderPosition;
            this.maxCycleDistance = maxCycleDistance;
        }

        public ParentResearchBroadcastPacket(ParentResearchBroadcastPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID, subscriptionPacket.senderAddress, subscriptionPacket.senderPosition, subscriptionPacket.senderRank, subscriptionPacket.senderRound, subscriptionPacket.cyclesDistance, subscriptionPacket.maxCycleDistance)
        {

        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new ParentResearchBroadcastPacket(received as ParentResearchBroadcastPacket);
        }
    }

    public class ParentConnectionRequestPacket : AbstractNetworkPacket, IRespondable
    {
        public INetworkPacket packet => this;

        public string senderAdress { get; set; }
        public int parentRank { get; set; }
        public int childrenRoundAtRequest { get; set; }
        public int parentChildrenCount { get; set; }
        public Vector3 parentPosition { get; set; }

        public ParentConnectionRequestPacket() { }

        public ParentConnectionRequestPacket(Vector3 parentPosition, int parentRank, int parentChildrenCount, int childrenRoundAtRequest)
        {
            this.parentPosition = parentPosition;
            this.parentRank = parentRank;
            this.parentChildrenCount = parentChildrenCount;
            this.childrenRoundAtRequest = childrenRoundAtRequest;   
        }

        public ParentConnectionRequestPacket(ParentConnectionRequestPacket subscriptionPacket) :
            this(subscriptionPacket.parentPosition, subscriptionPacket.parentRank, subscriptionPacket.parentChildrenCount, subscriptionPacket.childrenRoundAtRequest)
        {

        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new ParentConnectionRequestPacket(received as ParentConnectionRequestPacket);
        }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new ParentConnectionResponsePacket();
        }
    }


    public class ParentConnectionResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set; }

        public INetworkPacket packet => this;

        public int requestPing { get; set; }

        public int senderRound { get; set; }

        public bool response { get; set; }

        public ParentConnectionResponsePacket()
        {
        }
    }

    public class ChildrenSearchActivationPacket : AbstractNetworkPacket
    {

    }


    public class ChildrenValidationPacket : AbstractNetworkPacket, IRespondable
    {
        public INetworkPacket packet => this;

        public string senderAdress { get; set; }

        public ChildrenValidationPacket() { }

        public ChildrenValidationPacket(ChildrenValidationPacket subscriptionPacket) :
            this()
        {

        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new ChildrenValidationPacket(received as ChildrenValidationPacket);
        }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new ChildrenValidationResponsePacket();
        }
    }

    public class ChildrenValidationResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set; }

        public INetworkPacket packet => this;

        public int requestPing { get; set; }



        public ChildrenValidationResponsePacket()
        {
        }
    }
}
