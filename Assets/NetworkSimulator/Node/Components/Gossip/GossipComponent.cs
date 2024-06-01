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
        private List<IGossipPacket> _outgoingPacketsBuffer = new List<IGossipPacket>();
        private Dictionary<Type, IGossipDataHandler> _handlers = new Dictionary<Type, IGossipDataHandler>();
        private Action _onGossipPreUpdate;

        public void OnInitialize()
        {
            _gossipDelay = 1f / _gossipRate;
            var variance = (_gossipDelay / 20);
            _gossipDelay += UnityEngine.Random.Range(-variance, variance);
        }

        public void OnUpdate()
        {
            /*_timer += Time.deltaTime;

            if (_timer < _gossipDelay)
                return;

            ProcessGossipPackets();
            _timer = 0;*/
        }

        void Update()
        {
            _timer += Time.deltaTime;

            if (_timer < _gossipDelay)
                return;

            // call the pre processing Action, that other component can register
            _onGossipPreUpdate?.Invoke();

            // send the buffer to the broadcaster
            ProcessGossipPackets();

            _timer = 0;
        }

        /// <summary>
        /// Registers a gossip handler, an implementation that should holds the logic about one gossiped data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="gossipDataHandler"></param>
        public void RegisterGossipHandler<T>(IGossipDataHandler gossipDataHandler) where T : IGossipPacket
        {
            _handlers.Add(typeof(T), gossipDataHandler);
            _broadcaster.RegisterPacketHandlerWithMiddleware<T>(OnReceiveGossipPacket);
        }

        /// <summary>
        /// Register a callback that will be called before gossip processing on each round
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterGossipPreUpdateCallback(Action callback)
        {
            _onGossipPreUpdate += callback;
        }

        /// <summary>
        /// Adds a broadcastable gossip message to the component.
        /// The message will be send on the next gossip round via broadcasting to random other nodes.
        /// </summary>
        /// <param name="packet"></param>
        public void BufferAdd(IGossipPacket packet)
        {
            // processing the generation index increase here (this value is about tracking how much time a gossip packet has been relayed, and might be discarded above some value)
            packet.gossipGeneration++;

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

        // Search for a handler about a given IGossipPacket and relay the reception to it
        private void OnReceiveGossipPacket<T>(T networkPacket) where T : IGossipPacket
        {
            if (_handlers.TryGetValue(networkPacket.GetType(), out var gossipHandler))
            {
                var handler = (gossipHandler as IGossipDataHandler<T>);
                handler.OnReceiveGossip(this, (T)networkPacket);//Convert.ChangeType(networkPacket, networkPacket.GetType()));  ;
            }
        }
    }
}
