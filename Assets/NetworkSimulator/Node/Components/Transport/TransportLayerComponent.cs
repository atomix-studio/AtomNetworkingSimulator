using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.UIElements;
using System.Net;
using System.Threading;

namespace Atom.Transport
{
    public class TransportLayerComponent : MonoBehaviour, INodeComponent
    {
        public bool IsSleeping = false;
        public float MessageSpeed = 25;

        private Action<INetworkPacket> _routerReceiveCallback { get; set; }

        //debug only
        private Dictionary<INetworkPacket, NodeEntity> currentTravellingPacketTarget = new Dictionary<INetworkPacket, NodeEntity>();
        private Dictionary<INetworkPacket, float> currentTravellingPacketsElapsedTime = new Dictionary<INetworkPacket, float>();
        private Dictionary<INetworkPacket, float> currentTravellingPacketsTime = new Dictionary<INetworkPacket, float>();
        private Dictionary<INetworkPacket, Vector3> currentTravellingPacketsDestination = new Dictionary<INetworkPacket, Vector3>();
        //debug only
        private Dictionary<INetworkPacket, NodeEntity> _sendBuffer = new Dictionary<INetworkPacket, NodeEntity>();
        //debug only
        private Dictionary<INetworkPacket, NodeEntity> currentTravellingBytesPacketTarget = new Dictionary<INetworkPacket, NodeEntity>();
        private Dictionary<INetworkPacket, float> currentTravellingBytesPacketsElapsedTime = new Dictionary<INetworkPacket, float>();
        private Dictionary<INetworkPacket, float> currentTravellingBytesPacketsTime = new Dictionary<INetworkPacket, float>();
        private Dictionary<INetworkPacket, Vector3> currentTravellingBytesPacketsDestination = new Dictionary<INetworkPacket, Vector3>();


        public NodeEntity controller { get; set; }
        [Inject] private WorldSimulationManager _simulationManager;

        public void OnInitialize()
        {
            
        }

        /// <summary>
        /// Initialize the routing of the packets received by the transport layer to a delegate routing service
        /// </summary>
        /// <param name="routerReceiveCallback"></param>
        public void SetRoutingCallback(Action<INetworkPacket> routerReceiveCallback)
        {
            this._routerReceiveCallback = routerReceiveCallback;
        }

        public void Send(string address, INetworkPacket packet)
        {
            if (IsSleeping)
                return;

            var destination = WorldSimulationManager.nodeAddresses[address];
            WorldSimulationManager._totalPacketSent++;
            WorldSimulationManager._totalPacketSentPerSecondCount++;

            if (_simulationManager.TransportInstantaneously)
            {
                // to avoid overflowing the memory, nodes are frame buffered in instant transport mode
                _sendBuffer.Add(packet, destination);
            }
            else if(_simulationManager.TransportUnserialized)
            {
                // packet transport is simulated with just a timer (and visualised as a debug line)
                _addPacket(destination, packet);
            }
            else
            {

            }
        }

        #region simulation with serialization
        private void _addBytesPacket(NodeEntity target, INetworkPacket packet)
        {
            currentTravellingBytesPacketTarget.Add(packet, target);
            currentTravellingBytesPacketsDestination.Add(packet, target.transform.position);
            float ttime = Vector3.Distance(target.transform.position, transform.position) / _simulationManager.PacketSpeed;
            currentTravellingBytesPacketsTime.Add(packet, ttime);
            currentTravellingBytesPacketsElapsedTime.Add(packet, 0);
        }

        private void _removeBytesPacket(INetworkPacket packet)
        {
            currentTravellingBytesPacketTarget.Remove(packet);
            currentTravellingBytesPacketsDestination.Remove(packet);
            currentTravellingBytesPacketsElapsedTime.Remove(packet);
            currentTravellingBytesPacketsTime.Remove(packet);

            // packets are disposed when their job is done 
            packet.DisposePacket();
        }

        private void _updateBytesTravellingPackets()
        {
            var pos = transform.position;
            for (int i = 0; i < currentTravellingBytesPacketsElapsedTime.Count; ++i)
            {
                var packet = currentTravellingBytesPacketsElapsedTime.ElementAt(i);
                var direction = currentTravellingBytesPacketsDestination[packet.Key] - pos;
                float ratio = currentTravellingBytesPacketsElapsedTime[packet.Key] / currentTravellingBytesPacketsTime[packet.Key];
                // 
                if (_simulationManager.DisplayPackets)
                    Debug.DrawLine(transform.position, Vector3.Lerp(pos, currentTravellingBytesPacketsDestination[packet.Key], ratio), Color.blue);

                if (ratio >= 1f)
                {
                    if (currentTravellingBytesPacketTarget[packet.Key].gameObject.activeSelf)
                    {
                        WorldSimulationManager._totalPacketReceived++;
                        WorldSimulationManager._totalPacketReceivedPerSecondCount++;

                        currentTravellingBytesPacketTarget[packet.Key].transportLayer._routerReceiveCallback.Invoke(packet.Key);
                    }

                    _removePacket(packet.Key);
                    i--;
                }
                else
                {
                    direction.Normalize();
                    currentTravellingBytesPacketsElapsedTime[packet.Key] += Time.deltaTime * _simulationManager.PacketSpeed;
                }
            }
        }

        #endregion
        #region simulation no serialization
        // add a packet to the collections that simulates the network travelling
        private void _addPacket(NodeEntity target, INetworkPacket packet)
        {
            currentTravellingPacketTarget.Add(packet, target);
            currentTravellingPacketsDestination.Add(packet, target.transform.position);
            float ttime = Vector3.Distance(target.transform.position, transform.position) / _simulationManager.PacketSpeed;
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
                if (_simulationManager.DisplayPackets)
                    Debug.DrawLine(transform.position, Vector3.Lerp(pos, currentTravellingPacketsDestination[packet.Key], ratio), Color.blue);

                if ( ratio >= 1f)
                {
                    if (currentTravellingPacketTarget[packet.Key].gameObject.activeSelf)
                    {
                        WorldSimulationManager._totalPacketReceived++;
                        WorldSimulationManager._totalPacketReceivedPerSecondCount++;

                        currentTravellingPacketTarget[packet.Key].transportLayer._routerReceiveCallback.Invoke(packet.Key);
                    }

                    _removePacket(packet.Key);
                    i--;
                }
                else
                {
                    direction.Normalize();
                    currentTravellingPacketsElapsedTime[packet.Key] += Time.deltaTime * _simulationManager.PacketSpeed;
                }
            }
        }
        #endregion

        #region simulation with serialization



        #endregion

        public void OnUpdate()
        {
            //_updateTravellingPackets();
        }

        private void Update()
        {
            _updateTravellingPackets();
        }

        private void LateUpdate()
        {
            foreach (var packet in _sendBuffer)
            {
                packet.Value.transportLayer._routerReceiveCallback.Invoke(packet.Key);
            }

            _sendBuffer.Clear();
        }

    }

}

