using Atom.CommunicationSystem;
using Atom.ComponentProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.UIElements;

namespace Atom.Transport
{
    public class TransportLayerComponent : MonoBehaviour, INodeUpdatableComponent
    {
        public bool IsSleeping = false;
        public float MessageSpeed = 25;

        [SerializeField] private NetworkPacket _pf_networkPacket;
        private NodeEntity _nodeEntity;

        public Dictionary<string, Action<NetworkPacket>> BroadcastsRelayDelegates = new Dictionary<string, Action<NetworkPacket>>();
        public Dictionary<string, Action<NetworkPacket>> BroadcastsCallbackDelegates = new Dictionary<string, Action<NetworkPacket>>();

        public List<NetworkPacket> travellingPackets = new List<NetworkPacket>();

        public Action<INetworkPacket> routerReceiveCallback { get; protected set; }


        private Dictionary<INetworkPacket, NodeEntity> currentTravellingPacketTarget = new Dictionary<INetworkPacket, NodeEntity>();
        private Dictionary<INetworkPacket, float> currentTravellingPacketsElapsedTime = new Dictionary<INetworkPacket, float>();
        private Dictionary<INetworkPacket, float> currentTravellingPacketsTime = new Dictionary<INetworkPacket, float>();
        private Dictionary<INetworkPacket, Vector3> currentTravellingPacketsDestination = new Dictionary<INetworkPacket, Vector3>();

        public NodeEntity context { get; set; }
        private WorldSimulationManager _simulationManager;

        void Awake()
        {
            _simulationManager = WorldSimulationManager.Instance;
        }
        /// <summary>
        /// Initialize the routing of the packets received by the transport layer to a delegate routing service
        /// </summary>
        /// <param name="routerReceiveCallback"></param>
        public void SetRoutingCallback(Action<INetworkPacket> routerReceiveCallback)
        {
            this.routerReceiveCallback = routerReceiveCallback;
        }

        public void RegisterEndpoint(string name, Action<NetworkPacket> relayCallback)
        {
            BroadcastsRelayDelegates.Add(name, relayCallback);
        }

        public void OnInitialize()
        {
            _nodeEntity = context;
            _simulationManager = WorldSimulationManager.Instance;
        }


        public void Send(string address, INetworkPacket packet)
        {
            if (IsSleeping)
                return;

            var destination = WorldSimulationManager.nodeAddresses[address];
            WorldSimulationManager._totalPacketSent++;
            WorldSimulationManager._totalPacketSentPerSecondCount++;

            if (WorldSimulationManager.transportInstantaneously)
            {

                destination.transportLayer.routerReceiveCallback.Invoke(packet);

            }
            else
            {
                _addPacket(destination, packet);
            }
        }

        // add a packet to the collections that simulates the network travelling
        private void _addPacket(NodeEntity target, INetworkPacket packet)
        {
            currentTravellingPacketTarget.Add(packet, target);
            currentTravellingPacketsDestination.Add(packet, target.transform.position);
            float ttime = Vector3.Distance(target.transform.position, transform.position) / WorldSimulationManager.packetSpeed;
            currentTravellingPacketsTime.Add(packet, ttime) ;
            currentTravellingPacketsElapsedTime.Add(packet, 0);
        }

        private void _removePacket(INetworkPacket packet)
        {
            currentTravellingPacketTarget.Remove(packet);
            currentTravellingPacketsDestination.Remove(packet);
            currentTravellingPacketsElapsedTime.Remove(packet);
            currentTravellingPacketsTime.Remove(packet);

            // packets are disposed when their job is done 
            packet.DisposePacket();
        }

        private void _updateTravellingPackets()
        {
            var pos = transform.position;
            for (int i = 0; i < currentTravellingPacketsElapsedTime.Count; ++i)
            {
                var packet = currentTravellingPacketsElapsedTime.ElementAt(i);
                var direction = currentTravellingPacketsDestination[packet.Key] - pos;
                float ratio = currentTravellingPacketsElapsedTime[packet.Key] / currentTravellingPacketsTime[packet.Key];
                // 
                if (WorldSimulationManager.displayPackets)
                    Debug.DrawLine(transform.position, Vector3.Lerp(pos, currentTravellingPacketsDestination[packet.Key], ratio), Color.blue);

                if ( ratio >= 1f)
                {
                    if (currentTravellingPacketTarget[packet.Key].gameObject.activeSelf)
                    {
                        WorldSimulationManager._totalPacketReceived++;
                        WorldSimulationManager._totalPacketReceivedPerSecondCount++;

                        currentTravellingPacketTarget[packet.Key].transportLayer.routerReceiveCallback.Invoke(packet.Key);
                    }

                    _removePacket(packet.Key);
                    i--;
                }
                else
                {
                    direction.Normalize();
                    currentTravellingPacketsElapsedTime[packet.Key] += Time.deltaTime * WorldSimulationManager.packetSpeed;
                }
            }
        }

        public void OnUpdate()
        {
            //_updateTravellingPackets();
        }

        // *************************************

        #region V1 

        private void Update()
        {
            /* for (int i = 0; i < travellingPackets.Count; ++i)
                 travellingPackets[i].OnUpdate();*/


            _updateTravellingPackets();

        }

        public void SendPacket(NodeEntity target, string payload, List<NodeEntity> potentialPeers = null)
        {
            var packet = new NetworkPacket(this);
            packet._potentialPeers = potentialPeers;
            travellingPackets.Add(packet);
            packet.Send(_nodeEntity, target, payload);
        }

        public void SendPacketBroadcast(NodeEntity broadcaster, NodeEntity target, string payload, int broadcastID = -1)
        {
            var packet = new NetworkPacket(this);
            travellingPackets.Add(packet);
            packet.SendBroadcast(broadcaster, _nodeEntity, target, payload, broadcastID);
        }

        public void OnPacketReceived(NetworkPacket message)
        {
            if (IsSleeping)
                return;

            if (BroadcastsRelayDelegates.ContainsKey(message.Payload))
            {
                BroadcastsRelayDelegates[message.Payload].Invoke(message);
                return;
            }

            if (message.Payload == "BROADCAST_BENCHMARK")
            {
                _nodeEntity.material.color = Color.red;
                _nodeEntity.peerSampling.OnReceive_BenchmarkBroadcast(message);
                return;
            }

            Debug.Log($"{this.gameObject} received message {message.Payload} from {message.Sender}");

            /*if (message.Payload == "CONNECT_TO_CLUSTER")
            {
                if (!_nodeEntity.IsBoot)
                    Debug.LogError("Connection to cluster received by a non-boot node !");

                SendPacket(message.Sender, "CONNECT_TO_CLUSTER_RESPONSE");
            }
            else if (message.Payload == "CONNECT_TO_CLUSTER_RESPONSE")
            {
                _nodeEntity.peerSampling.OnReceiveConnectToClusterResponse(message.Sender);
            }*/
            if (message.Payload == "GROUP_REQUEST")
            {
                _nodeEntity.OnReceiveGroupRequest(message.Sender);
            }
            else if (message.Payload == "GROUP_REQUEST_REFUSED")
            {
                // faire de la place pour trouver un groupe
                _nodeEntity.peerSampling.OnGroupRequestRefused(message);
            }
            else
            {
                Debug.LogError("Packet unhandled : " + message.Payload);
            }
        }

        #endregion
    }

}

