using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.HierarchicalTree
{
    public class RankedConnectionSearchBroadcastPacket : AbstractNetworkPacket, IBroadcastablePacket
    {
        public long broadcasterID { get; set; }
        public long broadcastID { get; set; }
        public int senderRank { get; set; }
        public int relayCount { get; set; }
        public string senderAddress { get; set; }
        public int senderRound { get; set; }

        public RankedConnectionSearchBroadcastPacket() { }

        public RankedConnectionSearchBroadcastPacket(short packetIdentifier, long senderID, DateTime sentTime, long broadcastID, long broadcasterID, string senderAddress, int senderRank, int senderRound, int relayCount)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.senderRank = senderRank;
            this.senderAddress = senderAddress;
            this.senderRound = senderRound;
            this.relayCount = relayCount;
        }

        public RankedConnectionSearchBroadcastPacket(RankedConnectionSearchBroadcastPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID, subscriptionPacket.senderAddress, subscriptionPacket.senderRank, subscriptionPacket.senderRound, subscriptionPacket.relayCount)
        {

        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new RankedConnectionSearchBroadcastPacket(received as RankedConnectionSearchBroadcastPacket);
        }
    }

    public class RankedConnectingRequestPacket : AbstractNetworkPacket, IRespondable
    {

        public INetworkPacket packet => this;

        public string senderAdress { get; set; }
        public int senderRank { get; set; }
        public int senderRound { get; set; }
        public RankedConnectingRequestPacket() { }

        public RankedConnectingRequestPacket(int senderRank, int senderRound)
        {
            this.senderRank = senderRank;
            this.senderRound = senderRound;   
        }

        public RankedConnectingRequestPacket(RankedConnectingRequestPacket subscriptionPacket) :
            this(subscriptionPacket.senderRank, subscriptionPacket.senderRound)
        {

        }

        public INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new RankedConnectingRequestPacket(received as RankedConnectingRequestPacket);
        }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new RankedConnectionResponsePacket();
        }
    }


    public class RankedConnectionResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set; }

        public INetworkPacket packet => this;

        public int requestPing { get; set; }

        public int senderRound { get; set; }


        public RankedConnectionResponsePacket()
        {
        }
    }
}
