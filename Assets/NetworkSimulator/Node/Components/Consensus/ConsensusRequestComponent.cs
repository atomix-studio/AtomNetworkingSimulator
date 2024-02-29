﻿using Atom.DependencyProvider;
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

        private Dictionary<string, IConsensusPacket> _runningConsensuses = new Dictionary<string, IConsensusPacket>();
        [SerializeField, ReadOnly] private ColorVotingConsensusPacket _colorConsensusBuffer;

        [SerializeField] private float _voteTimeOut = 30; // secondes
        [SerializeField] private float _gossipFrequency = 4; // ticks per second
        private float _timer;
        private float _time;
        private bool _hasPending = false;

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
                if (_hasPending)
                {
                    Gossip();
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

                if (_runningConsensuses.TryGetValue(colorVote.consensusId, out var consensusPacket))
                {
                    var colorConsensusOrigin = new ColorVotingConsensusPacket((ColorVotingConsensusPacket)consensusPacket);

                    //if (!colorConsensusOrigin._alreadyVoted.Contains(colorVote.broadcasterID))
                    {
                        colorConsensusOrigin.consensusVersion++;
                        colorConsensusOrigin.Aggregate(colorVote);
                        colorConsensusOrigin._alreadyVoted.Add(colorVote.broadcasterID);

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
                    }

                    _hasPending = true;
                    // forwarding other votes
                    //_broadcaster.RelayBroadcast(colorVote);
                }
                else
                {
                    // choosing a response here
                    _colorConsensusBuffer = new ColorVotingConsensusPacket(colorVote);
                    _runningConsensuses.Add(_colorConsensusBuffer.consensusId, _colorConsensusBuffer);
                    var newVote = new ColorVotingConsensusPacket(_colorConsensusBuffer);
                    newVote.SelectChoice();

                    // broadcasting the local node vote first time we receive
                    _broadcaster.SendMulticast(newVote);
                }
            });
        }

        private void Gossip()
        {
            _hasPending = false;

            if (_colorConsensusBuffer.sentTime.AddSeconds(_voteTimeOut) < DateTime.Now)
            {
                Debug.LogError("Gossip timedout");
                _runningConsensuses.Remove(_colorConsensusBuffer.consensusId);
                return;
            }

            _broadcaster.SendMulticast(new ColorVotingConsensusPacket(_colorConsensusBuffer));
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
