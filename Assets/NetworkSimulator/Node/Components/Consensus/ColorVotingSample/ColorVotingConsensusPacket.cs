using Atom.CommunicationSystem;
using Atom.Helpers;
using Atom.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Broadcasting.Consensus
{
    [Serializable]
    public class ColorVotingConsensusPacket : AbstractConsensusPacket
    {
        // the vote of the running packet
        public int ColorSelection { get; set; }

        // a class variable not used in packet but used to holds the aggregated datas
        public int[] AggregatedSelections { get; set; } = new int[4];
        [SerializerIgnore] public int LocalSelection = -1;
        public List<long> _alreadyVoted = new List<long>();

        public ColorVotingConsensusPacket() { }

        public override void Aggregate(IConsensusPacket packet)
        {            
            var colorVote = (ColorVotingConsensusPacket)packet;
            AggregatedSelections[colorVote.ColorSelection]++;
        }

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            var forwardable = new ColorVotingConsensusPacket(received as ColorVotingConsensusPacket);
            return forwardable;
        }

        public ColorVotingConsensusPacket(short packetIdentifier, long senderID, DateTime sentTime, long broadcastID, long broadcasterID, int colorSelection, DateTime consensusStartedTime, long consensusId, int consensusVersion, int[] aggregatedSelections)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.concensusStartedTime = consensusStartedTime;
            this.consensusId = consensusId;
            this.consensusVersion = consensusVersion++;
            this.ColorSelection = colorSelection;
            this.AggregatedSelections = aggregatedSelections;
        }

        public ColorVotingConsensusPacket(ColorVotingConsensusPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID, subscriptionPacket.ColorSelection, subscriptionPacket.concensusStartedTime, subscriptionPacket.consensusId, subscriptionPacket.consensusVersion, subscriptionPacket.AggregatedSelections)
        {

        }

        public override void SelectChoice()
        {
            LocalSelection = NodeRandom.Range(0, 4);
            ColorSelection = LocalSelection;
        }
    }
}
