using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.Gossip
{
    /// <summary>
    /// Basically, gossip component is a buffer for datas that have to be shared 
    /// Other systems can relay datas a 'gossipable datas' by giving GossipComponent a packet 
    /// </summary>
    public class GossipComponent : MonoBehaviour, INodeUpdatableComponent
    {
        public NodeEntity controller { get ; set ; }

        /// <summary>
        /// number of gossip ticks per minute
        /// </summary>
        [SerializeField] private float _gossipRate = 3;

        private float _timer = 0;
        private float _gossipDelay;

        // every gossip round, a node will receive packets from the network, handle it, and decide wheter to gossip some packets (or not)
        // the packets will be created along the way and stored in the buffer until the next round happens 
        private List<IBroadcastablePacket> _outgoingPacketsBuffer = new List<IBroadcastablePacket>();

        public void OnInitialize()
        {
            _gossipDelay = 1f / _gossipRate;    
        }

        public void OnUpdate()
        {
            _timer += Time.deltaTime;

            if (_timer < _gossipDelay)
                return;

            ProcessGossipPackets();
            _timer = 0;
        }

        private void ProcessGossipPackets()
        {

        }
    }
}
