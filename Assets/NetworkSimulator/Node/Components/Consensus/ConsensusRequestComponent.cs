using Atom.ComponentProvider;
using Atom.Helpers;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Broadcasting.Consensus
{
    public class ConsensusRequestComponent : MonoBehaviour, INodeComponent
    {
        public NodeEntity context { get; set; }
        [InjectComponent] private BroadcasterComponent _broadcaster;

        private Dictionary<string, IConsensusPacket> _runningConsensuses = new Dictionary<string, IConsensusPacket>();
        [SerializeField, ReadOnly] private ColorVotingConsensusPacket _colorConsensusBuffer;

        public void OnInitialize()
        {
            // sample
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(ColorVotingConsensusPacket), (received) =>
            {
                var colorVote = (ColorVotingConsensusPacket)received;

                if (_runningConsensuses.TryGetValue(colorVote.consensusId, out var consensusPacket))
                {
                    consensusPacket.consensusVersion++;
                    consensusPacket.Aggregate(consensusPacket);

                    var colorConsensusOrigin = (ColorVotingConsensusPacket)consensusPacket;
                    int max = 0;
                    int maxIndex = -1;

                    for(int i = 0; i <  colorConsensusOrigin.AggregatedSelections.Length; ++i)
                    {
                        if (colorConsensusOrigin.AggregatedSelections[i] > max)
                        {
                            max = colorConsensusOrigin.AggregatedSelections[i];
                            maxIndex = i;
                        }
                    }

                    switch (maxIndex)
                    {
                        case 0:
                            context.material.color = Color.white;
                            break;
                        case 1:
                            context.material.color = Color.green;
                            break;
                        case 2:
                            context.material.color = Color.red;
                            break;
                        case 3:
                            context.material.color = Color.blue;
                            break;
                    }

                    // forwarding other votes
                    _broadcaster.RelayBroadcast(colorVote);
                }
                else
                {
                    // choosing a response here
                    _colorConsensusBuffer = colorVote;
                    _colorConsensusBuffer.SelectChoice();
                    _runningConsensuses.Add(colorVote.consensusId, colorVote);

                    // broadcasting the local node vote first time we receive
                    _broadcaster.RelayBroadcast(colorVote);
                }
            });
        }

        public void StartBroadcastConsensusPacket(IConsensusPacket consensusPacket)
        {
            consensusPacket.consensusId = System.Guid.NewGuid().ToString();
            consensusPacket.consensusVersion = 0; // obvious but for the sake of explicity

            _runningConsensuses.Add(consensusPacket.consensusId, consensusPacket);
            // locally voting
            consensusPacket.SelectChoice();
            consensusPacket.Aggregate(consensusPacket);

            _broadcaster.SendBroadcast(consensusPacket);
        }

        public void StartColorVoting()
        {
            var colorVotingPacket = new ColorVotingConsensusPacket();
            StartBroadcastConsensusPacket(colorVotingPacket);
        }
    }
}
