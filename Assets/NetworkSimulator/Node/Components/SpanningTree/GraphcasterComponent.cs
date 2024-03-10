using Atom.Broadcasting;
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

namespace Atom.Components.GraphNetwork
{
    public class GraphcasterComponent : MonoBehaviour, INodeComponent
    {
        [Inject] private PacketRouter _packetRouter;
        [Inject] private GraphEntityComponent _graphEntityComponent;
        [Inject] private NetworkConnectionsComponent _networkConnections;

        // how many graphcast id the node will keep in memory 
        // replace over time when buffer is full
        private int _relayedGraphcastsBufferSize = 1024;

        private Dictionary<long, int> _relayedGraphcastsBuffer;
        public Dictionary<long, int> relayedGraphcastsBuffer => _relayedGraphcastsBuffer;

        public NodeEntity controller { get; set; }

        private DateTime _lastReceivedGraphcast;

        public void OnInitialize()
        {
            _relayedGraphcastsBuffer = new Dictionary<long, int>();
            RegisterGraphcast(typeof(GraphcastBenchmarkPacket), ReceivedBenchmarkGraphcast, true);
        }

        [Button]
        private void SendBenchmarkGraphcast()
        {
            SendGraphcast(new GraphcastBenchmarkPacket());
        }

        private void ReceivedBenchmarkGraphcast(INetworkPacket packet)
        {
            Debug.Log("Received graphcast benchmark");
        }

        public void RegisterGraphcast(Type packetType, Action<INetworkPacket> packetReceiveHandler, bool relayGraphcastAutomatically)
        {
            _packetRouter.RegisterPacketHandler(packetType, (receivedPacket) =>
            {
                // the graphcaster keeps in memory this data 
                // it will be used to detect if a node has became isolated from the graph and should begin a REJOIN procedure (graph recovery component)
                _lastReceivedGraphcast = DateTime.Now; 

                // the router checks for packet that are IBroadcastable and ensure they haven't been too much relayed
                // if the packet as reached is maximum broadcast cycles on the node and the router receives it one more time
                if (receivedPacket is IBroadcastablePacket)
                {
                    var broadcastable = (IBroadcastablePacket)receivedPacket;
                    if (!CheckGraphcastRelayCycles(broadcastable))
                        return;

                    /*for (int i = 0; i < _receivedBroadcastMiddlewares.Count; ++i)
                    {
                        if (!_receivedBroadcastMiddlewares[i](broadcastable))
                            return;
                    }*/

                    packetReceiveHandler?.Invoke(receivedPacket);

                    if (relayGraphcastAutomatically)
                        RelayGraphcast(broadcastable);
                }
                else
                {
                    packetReceiveHandler?.Invoke(receivedPacket);
                }
            },
            true);
        }

        /// <summary>
        /// Sends a packet to all known graph edges
        /// Graphcasts uses the IBroadcastable because the broadcaster will use the graphcaster if the network is under MST 
        /// </summary>
        /// <param name="broadcastable"></param>
        public void SendGraphcast(IBroadcastablePacket broadcastable)
        {
            broadcastable.broadcasterID = _networkConnections.LocalPeerInfo.peerID;
            broadcastable.broadcastID = NodeRandom.UniqueID();// Guid.NewGuid().ToString();

            INetworkPacket current = broadcastable;

            for (int i = 0; i < _graphEntityComponent.graphEdges.Count; ++i)
            {
                // create a new packet from the received and forwards it to the router
                _packetRouter.Send(_graphEntityComponent.graphEdges[i].EdgeAdress, current);

                if (i < _graphEntityComponent.graphEdges.Count - 1)
                    current = broadcastable.ClonePacket(current);
            }
        }

        /// <summary>
        /// Handle the relaying/forwarding of a received broadcasted-type packet
        /// </summary>
        /// <param name="packet"></param>
        public void RelayGraphcast(IBroadcastablePacket broadcastable)
        {
            if (_graphEntityComponent.graphEdges.Count == 0)
                return;

            for (int i = 0; i < _graphEntityComponent.graphEdges.Count; ++i)
            {
                // create a new packet from the received and forwards it to the router
                var relayedPacket = broadcastable.ClonePacket(broadcastable);
                _packetRouter.Send(_graphEntityComponent.graphEdges[i].EdgeAdress, relayedPacket);
            }
        }

        // graphcasts can't be relayed more than once
        // cause our mst won't ever be perfect over a network, we avoid cycling by 
        // limiting sents for a given ID at 1
        private bool CheckGraphcastRelayCycles(IBroadcastablePacket packet)
        {
            if (_relayedGraphcastsBuffer.ContainsKey(packet.broadcastID))
            {
                _relayedGraphcastsBuffer[packet.broadcastID]++;

                if (_relayedGraphcastsBuffer[packet.broadcastID] > 1)
                    return false;

                return true;
            }
            else
            {
                if (_relayedGraphcastsBuffer.Count >= _relayedGraphcastsBufferSize)
                {
                    _relayedGraphcastsBuffer.Remove(_relayedGraphcastsBuffer.ElementAt(0).Key);
                }
                _relayedGraphcastsBuffer.Add(packet.broadcastID, 0);
                return true;
            }
        }
    }
}
