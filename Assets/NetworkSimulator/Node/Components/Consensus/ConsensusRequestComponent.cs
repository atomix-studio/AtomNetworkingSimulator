using Atom.CommunicationSystem;
using Atom.DependencyProvider;
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
    public class ConsensusRequestComponent : MonoBehaviour, INodeUpdatableComponent
    {
        public NodeEntity controller { get; set; }
        [Inject] private BroadcasterComponent _broadcaster;

        private Dictionary<long, RunningConsensusData> _runningConsensuses = new Dictionary<long, RunningConsensusData>();
        [SerializeField, ReadOnly] private ColorVotingConsensusPacket debugColorConsensus;

        [SerializeField] private float _voteTimeOut = 30; // secondes
        [SerializeField] private float _gossipFrequency = 4; // ticks per second
        private float _timer;
        private float _time;
        private bool _hasPending = false;

        public int[] debugAggregate = new int[4];
        private List<long> _endingConsensusesBuffer = new List<long>();

        public void OnUpdate()
        {


        }

        void Update()
        {
            if (_runningConsensuses.Count == 0)
                return;


            _time = 1f / _gossipFrequency;
            _timer += Time.deltaTime;

            if (_timer > _time)
            {
              
                foreach (var consensus in _runningConsensuses)
                {
                    // todo
                    // eventually remove it after few minutes
                    if (consensus.Value.hasExpired)
                        continue;

                    if (DateTime.Now > consensus.Value.expiration_time )
                    {
                        // we don't remove the consensus data at expiration because if we do, 
                        // we could receive a new consensus packet from another node and can't notice it was expired
                        consensus.Value.hasExpired = true;                 
                    }
                    else
                    {
                        if (consensus.Value.hasUpdate)
                            Gossip(consensus.Value);
                    }
                }

                for (int i = 0; i < _endingConsensusesBuffer.Count; ++i)
                {
                    Debug.LogError("Gossip timedout");
                    _runningConsensuses.Remove(_endingConsensusesBuffer[i]);
                    _endingConsensusesBuffer.RemoveAt(i);
                    i--;
                }

                _timer = 0;
            }
        }

        public void OnInitialize()
        {
            // sample
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(ColorVotingConsensusPacket), (received) =>
            {
                var colorVote = (ColorVotingConsensusPacket)received;

                if (_runningConsensuses.TryGetValue(colorVote.consensusId, out var consensusData))
                {
                    consensusData.hasUpdate = true;
                    var colorConsensusOrigin = consensusData.packet as ColorVotingConsensusPacket; // new ColorVotingConsensusPacket((ColorVotingConsensusPacket)consensusPacket.packet);

                    //if (!colorConsensusOrigin._alreadyVoted.Contains(colorVote.broadcasterID))
                    colorConsensusOrigin.consensusVersion++;
                    colorConsensusOrigin.Aggregate(colorVote);
                    colorConsensusOrigin._alreadyVoted.Add(colorVote.broadcasterID);
                    debugAggregate[colorVote.ColorSelection]++;

                    Debug.Log("version" + colorConsensusOrigin.consensusVersion);
                    int max = 0;
                    int maxIndex = -1;

                    for (int i = 0; i < colorConsensusOrigin.AggregatedSelections.Length; ++i)
                    {
                        if (colorConsensusOrigin.AggregatedSelections[i] > max)
                        {
                            max = colorConsensusOrigin.AggregatedSelections[i];
                            maxIndex = i;
                        }
                    }

                    Debug.Log(maxIndex);

                    switch (maxIndex)
                    {
                        case 0:
                            controller.material.color = Color.white;
                            break;
                        case 1:
                            controller.material.color = Color.green;
                            break;
                        case 2:
                            controller.material.color = Color.red;
                            break;
                        case 3:
                            controller.material.color = Color.blue;
                            break;
                    }

                    _hasPending = true;
                    // forwarding other votes
                    //_broadcaster.RelayBroadcast(colorVote);
                }
                else
                {
                    // choosing a response here
                    debugColorConsensus = new ColorVotingConsensusPacket(colorVote);
                    var newVote = new ColorVotingConsensusPacket(colorVote);
                    newVote.SelectChoice();
                    consensusData = new RunningConsensusData() { hasUpdate = true, packet = newVote, expiration_time = colorVote.concensusStartedTime.AddSeconds(_voteTimeOut) };
                    _runningConsensuses.Add(debugColorConsensus.consensusId, consensusData);

                    // packet with chosen vote from local node will be sent next gossip roung
                    /*// broadcasting the local node vote first time we receive
                    _broadcaster.SendMulticast(newVote);*/
                }
            });
        }

        private void Gossip(RunningConsensusData data)
        {
            /// FROM HERE, the packet should be given to the gossip component that will handle the sending

            _hasPending = false;

            var colorConsensusGossip = data.packet.ClonePacket(data.packet); //new ColorVotingConsensusPacket(debugColorConsensus);

            _broadcaster.SendMulticast(colorConsensusGossip);
            data.hasUpdate = false;
        }

        public void StartBroadcastConsensusPacket(IConsensusPacket consensusPacket)
        {
            consensusPacket.consensusId = NodeRandom.UniqueID(); // System.Guid.NewGuid().ToString();
            consensusPacket.consensusVersion = 0; // obvious but for the sake of explicity

            _runningConsensuses.Add(consensusPacket.consensusId, new RunningConsensusData() { hasUpdate = false, packet = consensusPacket });
            // locally voting
            consensusPacket.SelectChoice();
            consensusPacket.Aggregate(consensusPacket);

            consensusPacket.concensusStartedTime = DateTime.Now; 

            _broadcaster.SendBroadcast(consensusPacket);
        }

        public void StartColorVoting()
        {
            var colorVotingPacket = new ColorVotingConsensusPacket();
            StartBroadcastConsensusPacket(colorVotingPacket);
        }

    }
}
