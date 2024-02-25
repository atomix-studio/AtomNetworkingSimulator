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
        [SerializerIgnore] public int[] AggregatedSelections = new int[4];
        [SerializerIgnore] public int LocalSelection = -1;
        public HashSet<string> _alreadyVoted = new HashSet<string>();

        public ColorVotingConsensusPacket() { }

        public override void Aggregate(IConsensusPacket packet)
        {            
            var colorVote = (ColorVotingConsensusPacket)packet;
            AggregatedSelections[colorVote.ColorSelection]++;
        }

        public override INetworkPacket GetForwardablePacket(INetworkPacket received)
        {
            var forwardable = new ColorVotingConsensusPacket(received as ColorVotingConsensusPacket);
            return forwardable;
        }

        public ColorVotingConsensusPacket(short packetIdentifier, string senderID, DateTime sentTime, string broadcastID, string broadcasterID, int colorSelection, string consensusId, int consensusVersion)
        {
            this.packetTypeIdentifier = packetIdentifier;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcastID = broadcastID;
            this.broadcasterID = broadcasterID;
            this.consensusId = consensusId;
            this.consensusVersion = consensusVersion++;
            this.ColorSelection = colorSelection;
        }

        public ColorVotingConsensusPacket(ColorVotingConsensusPacket subscriptionPacket) :
            this(subscriptionPacket.packetTypeIdentifier, subscriptionPacket.senderID, subscriptionPacket.sentTime, subscriptionPacket.broadcastID, subscriptionPacket.broadcasterID, subscriptionPacket.ColorSelection, subscriptionPacket.consensusId, subscriptionPacket.consensusVersion)
        {

        }

        public override void SelectChoice()
        {
            LocalSelection = NodeRandom.Range(0, 4);
            ColorSelection = LocalSelection;
        }
    }
}
