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
using Unity.Mathematics;
using UnityEngine;

namespace Atom.Broadcasting
{
    public static class BroadcasterEventHandler
    {
        public delegate void BroadcastPacketRelayedHandler(IBroadcastablePacket packet);
        public static event BroadcastPacketRelayedHandler OnBroadcastPacketRelayed;
        public static void BroadcastPacketRelayed(IBroadcastablePacket packet) => OnBroadcastPacketRelayed?.Invoke(packet);
    }

    public class BroadcasterComponent : MonoBehaviour, INodeComponent
    {
        [Inject] private PacketRouter _router;
        [Inject] private NetworkHandlingComponent _networkHandling;

        [Header("Broadcasting Properties")]
        // number of calls for a broadcast
        [SerializeField] private int _fanout = 3;

        // number of possible re-forwarding of broadcast packet, identifier by a broadcast identifier that remains unchanged when forwarded by peers 
        [SerializeField] private int _broadcastRelayingCycles = 2;

        // if > 0, the number of cycles that the broadcasts sent by the local node can be relayed by others
        // this also can be overriden at the broadcast packet creation for specific cases
        [SerializeField] private int _broadcastMessageMaxCycles = -1;

        /// <summary>
        /// The buffer for relayed broadcast has a finite size to avoid overloading of the memory at runtime
        /// when the capacity is at its maximum the node will replace older values on the go and eventually request a global buffer clean event
        /// </summary>
        [SerializeField] private int _relayedBroadcastBufferSize = 2500;

        // variables
        private System.Random _random;
        private Dictionary<long, int> _relayedBroadcastsBuffer;

        public Dictionary<long, int> relayedBroadcastsBuffer => _relayedBroadcastsBuffer;
        private List<Func<IBroadcastablePacket, bool>> _receivedBroadcastMiddlewares = new List<Func<IBroadcastablePacket, bool>>();

        public NodeEntity controller { get; set; }

        public void OnInitialize()
        {
            _random = new System.Random((int)DateTime.Now.Ticks % int.MaxValue);
            _relayedBroadcastsBuffer = new Dictionary<long, int>();

            RegisterPacketHandlerWithMiddleware(typeof(BroadcastBenchmarkPacket), (received) =>
            {
                controller.material.color = Color.red;
                RelayBroadcast((IBroadcastablePacket)received);
            });
        }

        /// <summary>
        /// Registers a packet with the broadcast handling logic as middleware.
        /// Packets that have already been broadcasted > _broadcastRelayingCycles, the broadcaster will stop the message from being received by the upper layers (services) 
        /// </summary>
        /// <param name="packetType"></param>
        /// <param name="packetReceiveHandler"></param>
        public void RegisterPacketHandlerWithMiddleware(Type packetType, Action<INetworkPacket> packetReceiveHandler, bool relayBroadcastAutomatically = false)
        {
            _router.RegisterPacketHandler(packetType, (receivedPacket) =>
            {
                // the router checks for packet that are IBroadcastable and ensure they haven't been too much relayed
                // if the packet as reached is maximum broadcast cycles on the node and the router receives it one more time
                if (receivedPacket is IBroadcastablePacket)
                {
                    var broadcastable = (IBroadcastablePacket)receivedPacket;
                    if (!CheckBroadcastRelayCycles(broadcastable))
                        return;

                    for (int i = 0; i < _receivedBroadcastMiddlewares.Count; ++i)
                    {
                        if (!_receivedBroadcastMiddlewares[i](broadcastable))
                            return;
                    }

                    packetReceiveHandler?.Invoke(receivedPacket);

                    if (relayBroadcastAutomatically)
                        RelayBroadcast(broadcastable);
                }
                else
                {
                    packetReceiveHandler?.Invoke(receivedPacket);
                }
            },
            true);
        }

        public void RegisterBroadcastReceptionMiddleware(Func<IBroadcastablePacket, bool> routingMiddleware)
        {
            if (_receivedBroadcastMiddlewares == null)
                _receivedBroadcastMiddlewares = new List<Func<IBroadcastablePacket, bool>>();

            _receivedBroadcastMiddlewares.Add(routingMiddleware);
        }

        public void UnregisterBroadcastReceptionMiddleware(Func<IBroadcastablePacket, bool> routingMiddleware)
        {
            if (_receivedBroadcastMiddlewares == null)
                return;

            _receivedBroadcastMiddlewares.Add(routingMiddleware);
        }

        /// <summary>
        /// Sends a packet to a known receiver, contained in the PeerNetworkCore.Listenners dictionnary
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="receiver"></param>
        public void Send(string listennerAddrss, INetworkPacket packet)
        {
            _router.Send(listennerAddrss, packet);
        }

        /// <summary>
        /// A request is a packet sent that will be awaiting for a response callback in its context
        /// </summary>
        /// <param name="target"></param>
        /// <param name="networkPacket"></param>
        /// <param name="responseCallback"></param>
        public void SendRequest(string listennerAdress, INetworkPacket networkPacket, Action<INetworkPacket> responseCallback, int timeout_ms = 50000)
        {
            _router.SendRequest(listennerAdress, networkPacket, responseCallback, timeout_ms);
        }

        /// <summary>
        /// Sends a response to a request that will be handled by the request awaiter on the sender and not with the basic packet received handler
        /// </summary>
        /// <param name="callingPacket"></param>
        /// <param name="response"></param>
        public void SendResponse(IRespondable callingPacket, IResponse response)
        {
            _router.SendResponse(callingPacket, response);
        }

        /// <summary>
        /// Sends a packet to <_fanout> listenners, selected randomly (not a broadcast, the packets will just go to their receivers and won't be relayed)
        /// </summary>
        /// <param name="broadcastable"></param>
        public void SendMulticast(INetworkPacket packet, int fanoutOverride = -1)
        {
            if (_networkHandling.Connections.Count <= 0)
                return;

            var calls_count = GetCurrentFanout(fanoutOverride);
            var current = packet;
             // security here. 
            // it happens that broadcastable packet are forwared as multicast
            // if broadcasterID and broadcastID have been cloned or haven't been set, the relayed broadcast from nodes will be filled with string.Empty and the packet multicasted will encounter bugs.
            if (packet is IBroadcastablePacket)
            {
                var brdcst = packet as IBroadcastablePacket;
                brdcst.broadcasterID = _networkHandling.LocalPeerInfo.peerID;
                brdcst.broadcastID = _random.Next(-int.MaxValue, int.MaxValue);
            }

            for (int i = 0; i < calls_count; ++i)
            {
                var index = _random.Next(_networkHandling.Connections.Count);
                _router.Send(_networkHandling.Connections.ElementAt(index).Value.peerAdress, current);
                current = ((IClonablePacket)packet).ClonePacket(current);
            }
        }

        /// <summary>
        /// Sends a packet to <_fanout> listenners, selected randomly, that will be relayed until certain conditions are met (cycles, timeout, broadcast relay limit achieved over all network...)
        /// </summary>
        /// <param name="broadcastable"></param>
        public void SendBroadcast(IBroadcastablePacket broadcastable, int fanoutOverride = -1, int cyclesRange = -1)
        {
            if (_networkHandling.Connections.Count <= 0)
                return;

            // the work is now done by the packet router as it is the solo entry point for transport layer 
            // it is more secure to handle ids down that system cause if they aare unset for some reason,
            // it will screw up their ability to be received as they will be blocked by the broadcaster middleware of the receiver
            // (if two or 3 broadcastable packet arrives with a string.Empty id, all other broadcasts with string.Empty id will be ignored as well

            broadcastable.broadcasterID = _networkHandling.LocalPeerInfo.peerID;
            broadcastable.broadcastID = NodeRandom.UniqueID();// Guid.NewGuid().ToString();

            INetworkPacket current = broadcastable;

            var calls_count = GetCurrentFanout(fanoutOverride);
            for (int i = 0; i < calls_count; ++i)
            {
                var index = _random.Next(_networkHandling.Connections.Count);
                _router.Send(_networkHandling.Connections.ElementAt(index).Value.peerAdress, current);

                // cloning packet is done here cause we are in a local environment simulating network packets
                // the receiver is disposing packet and we surely don't want any strange reference bugs if multiple nodes share an instance of the same packet
                current = broadcastable.ClonePacket(current);
            }
        }

        /// <summary>
        /// Handle the relaying/forwarding of a received broadcasted-type packet
        /// </summary>
        /// <param name="packet"></param>
        public void RelayBroadcast(IBroadcastablePacket broadcastable, int fanoutOverride = -1)
        {
            // allow other service to handle whatever they need when a broadcast is received and (accepted)
            BroadcasterEventHandler.BroadcastPacketRelayed(broadcastable);

            // try register peer serait géré par une callback sur l'event dans le peer sampling service
            /* TryRegisterPeer(packet.Broadcaster);

             // the sender of a broadcast received by a node should always be in its Callers view 
              to remove TryRegisterPeer(packet.Sender);
 */
            // broadcast cannot be relayed if the node doesn't have any listenners to send it to
            if (_networkHandling.Connections.Count <= 0)
                return;

            var calls_count = GetCurrentFanout(fanoutOverride);

            for (int i = 0; i < calls_count; ++i)
            {
                var index = BroadcastingHelpers.GetRandomConnectionIndexForBroadcast(_networkHandling.Connections, broadcastable.broadcasterID, broadcastable.senderID, calls_count * 2);

                // create a new packet from the received and forwards it to the router
                var relayedPacket = broadcastable.ClonePacket(broadcastable);
                _router.Send(_networkHandling.Connections.ElementAt(index).Value.peerAdress, relayedPacket);
            }
        }

        private int GetCurrentFanout(int fanoutOverride = -1)
        {
            if (fanoutOverride > 0)
                return fanoutOverride;

            return _fanout > _networkHandling.Connections.Count ? _networkHandling.Connections.Count : _fanout;
        }

        /// <summary>
        /// Broadcast that are relayed by a node are kept in memory to allow node to limit the number of time they will relay the same broadcast when they receive it.
        /// This buffer will always keeps growing within the node lifetime and has to be clean sometime, depending on a logic that has to be decided
        /// The buffer could have a finite size and message will be replaced as time goes on, taking the oldest entries and considering they are old enough to be forgotten
        /// The cleaning of the buffer can also be a global event broadcasted randomly by a node after a voting process to select a decidor for sending the event ?
        /// </summary>
        public void ClearRelayedBroadcastsBuffer()
        {
            // we could imagine keeping the last broadcasts as they can be still alive at the time we delete it
            // but that's not a big deal if broadcasts are relayed twice their cycles on a random node sometime
            _relayedBroadcastsBuffer.Clear();
        }

        private bool CheckBroadcastRelayCycles(IBroadcastablePacket packet)
        {
            if (_relayedBroadcastsBuffer.ContainsKey(packet.broadcastID))
            {
                _relayedBroadcastsBuffer[packet.broadcastID]++;

                if (_relayedBroadcastsBuffer[packet.broadcastID] > _broadcastRelayingCycles)
                    return false;

                return true;
            }
            else
            {
                if (_relayedBroadcastsBuffer.Count >= _relayedBroadcastBufferSize)
                {
                    _relayedBroadcastsBuffer.Remove(_relayedBroadcastsBuffer.ElementAt(0).Key);
                }
                _relayedBroadcastsBuffer.Add(packet.broadcastID, 0);
                return true;
            }
        }

        public bool IsBroadcastForwardable(IBroadcastablePacket packet)
        {
            return !_relayedBroadcastsBuffer.ContainsKey(packet.broadcastID) || _relayedBroadcastsBuffer[packet.broadcastID] <= _broadcastRelayingCycles;
        }

        [Button]
        public void BroadcastBenchmark()
        {
            SendBroadcast(new BroadcastBenchmarkPacket());
        }

        /*        public void Broadcast(Protocol protocol, string PAYLOAD_NAME)
                {
                    if (AvalaiblePeers.Count <= 0)
                        return;

                    var count = Fanout > AvalaiblePeers.Count ? AvalaiblePeers.Count : Fanout;
                    for (int i = 0; i < count; ++i)
                    {
                        var index = random.Next(AvalaiblePeers.Count);
                        _nodeEntity.transportLayer.SendPacketBroadcast(_nodeEntity, AvalaiblePeers[index], protocol, PAYLOAD_NAME);
                    }
                }

                public void RelayBroadcast(NetworkPacket packet)
                {
                    if (_relayedBroadcasts.ContainsKey(packet.BroadcastID))
                    {
                        _relayedBroadcasts[packet.BroadcastID]++;

                        if (_relayedBroadcasts[packet.BroadcastID] > BroadcastForwardingMaxCycles)
                            return;
                    }
                    else
                    {
                        _relayedBroadcasts.Add(packet.BroadcastID, 0);
                    }

                    // empecher la boucle infinie des broadcasts
                    TryRegisterPeer(packet.Broadcaster);
                    // récupérer les infos "au passage" lors d'un broadcast permet d'alimenter plus rapidement les connections connues 
                    // limitant ainsi la nécessité pour elle d'envoyer des broadcast de découverte réseau
                    TryRegisterPeer(packet.Sender);

                    if (AvalaiblePeers.Count <= 0)
                        return;

                    var count = Fanout > AvalaiblePeers.Count ? AvalaiblePeers.Count : Fanout;

                    for (int i = 0; i < count; ++i)
                    {
                        var count_break = 0;
                        var index = 0;
                        do
                        {
                            index = random.Next(AvalaiblePeers.Count);
                            count_break++;

                            if (count_break > AvalaiblePeers.Count * 2)
                                break;
                        }
                        while (AvalaiblePeers[index] == packet.Broadcaster
                              || AvalaiblePeers[index] == packet.Sender);

                        _nodeEntity.transportLayer.SendPacketBroadcast(
                                           packet.Broadcaster,
                                           AvalaiblePeers[index],
                                           Protocol.HTTP,
                                           packet.Payload,
                                           // ICI on relaye bien l'identifiant unique du broadcast (ne pas confondre avec l'identifiant unique du message)
                                           // cela permet d'éviter de relayer en boucle un broadcast
                                           packet.BroadcastID);
                    }

                }
        */
    }
}
