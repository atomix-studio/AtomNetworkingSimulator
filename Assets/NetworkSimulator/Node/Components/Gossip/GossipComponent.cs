using Atom.Broadcasting;
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
        public NodeEntity controller { get; set; }

        [Inject] private BroadcasterComponent _broadcaster;

        /// <summary>
        /// number of gossip ticks per minute
        /// </summary>
        [SerializeField] private float _gossipRate = 3;

        private float _timer = 0;
        private float _gossipDelay;

        // every gossip round, a node will receive packets from the network, handle it, and decide wheter to gossip some packets (or not)
        // the packets will be created along the way and stored in the buffer until the next round happens 
        private List<IBroadcastablePacket> _outgoingPacketsBuffer = new List<IBroadcastablePacket>();
        private Dictionary<Type, IGossipDataHandler> _handlers = new Dictionary<Type, IGossipDataHandler>();

        public void OnInitialize()
        {
            _gossipDelay = 1f / _gossipRate;
            _broadcaster.RegisterBroadcastReceptionMiddleware(BroadcasterMiddlewareHandler);
        }

        private bool BroadcasterMiddlewareHandler(IBroadcastablePacket packet)
        {
            if(_handlers.TryGetValue(packet.GetType(), out var gossipHandler))
            {
                (gossipHandler as IGossipDataHandler<IBroadcastablePacket>).OnReceiveGossip((IBroadcastablePacket)Convert.ChangeType(packet, packet.GetType()));
            }

            return true;
        }

        public void OnUpdate()
        {
            _timer += Time.deltaTime;

            if (_timer < _gossipDelay)
                return;

            ProcessGossipPackets();
            _timer = 0;
        }

        public void RegisterGossipHandler<T>(IGossipDataHandler gossipDataHandler) where T : IBroadcastablePacket
        {
            _handlers.Add(typeof(T), gossipDataHandler);
        }

        /// <summary>
        /// Adds a broadcastable gossip message to the component.
        /// The message will be send on the next gossip round via broadcasting to random other nodes.
        /// </summary>
        /// <param name="packet"></param>
        public void BufferAdd(IBroadcastablePacket packet)
        {
            _outgoingPacketsBuffer.Add(packet);
        }

        private void ProcessGossipPackets()
        {
            if (_outgoingPacketsBuffer.Count == 0)
                return;

            for (int i = 0; i < _outgoingPacketsBuffer.Count; i++)
            {
                _broadcaster.SendBroadcast(_outgoingPacketsBuffer[i]);
            }

            // packets are disposed by transport layer after sending
            _outgoingPacketsBuffer.Clear();
        }
    }
}
