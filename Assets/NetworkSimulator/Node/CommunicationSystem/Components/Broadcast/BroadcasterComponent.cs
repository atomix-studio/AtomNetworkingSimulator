using Atom.CommunicationSystem;
using Atom.CommunicationSystem;
using Atom.ComponentSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace Atom.CommunicationSystem
{
    public static class BroadcasterEventHandler
    {
        public delegate void BroadcastPacketRelayedHandler(IBroadcastable packet);
        public static event BroadcastPacketRelayedHandler OnBroadcastPacketRelayed;
        public static void BroadcastPacketRelayed(IBroadcastable packet) => OnBroadcastPacketRelayed?.Invoke(packet);
    }

    public class BroadcasterComponent : MonoBehaviour, INodeComponent
    {
        [InjectNodeComponentDependency] private PacketRouter _router;

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
        private Dictionary<string, int> _relayedBroadcastsBuffer;

        public Dictionary<string, int> relayedBroadcastsBuffer => _relayedBroadcastsBuffer;

        public NodeEntity context { get; set; }

        public void OnInitialize()
        {
            _random = new System.Random((int)DateTime.Now.Ticks % int.MaxValue);
            _relayedBroadcastsBuffer = new Dictionary<string, int>();
        }

        /// <summary>
        /// Registers a packet
        /// </summary>
        /// <param name="packetType"></param>
        /// <param name="packetReceiveHandler"></param>
        public void RegisterPacketHandler(Type packetType, Action<INetworkPacket> packetReceiveHandler)
        {
            _router.RegisterPacketHandler(packetType, packetReceiveHandler);
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
        public void SendRequest(string listennerAddrss, INetworkPacket networkPacket, Action<INetworkPacket> responseCallback, int timeout_ms = 1000)
        {
            _router.SendRequest(listennerAddrss, networkPacket, responseCallback, timeout_ms);
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
        /// Sends a packet to <_fanout> listenners, selected randomly
        /// </summary>
        /// <param name="broadcastable"></param>
        public void SendBroadcast(IBroadcastable broadcastable)
        {
            if (context.networkHandling.Listenners.Count <= 0)
                return;

            var count = _fanout > context.networkHandling.Listenners.Count ? context.networkHandling.Listenners.Count : _fanout;
            for (int i = 0; i < count; ++i)
            {
                var index = _random.Next(count);
                _router.Send(context.networkHandling.Listenners.ElementAt(index).Value.peerAdress, broadcastable.packet);
            }
        }

        /// <summary>
        /// Handle the relaying/forwarding of a received broadcasted-type packet
        /// </summary>
        /// <param name="packet"></param>
        public void RelayBroadcast(IBroadcastable broadcastable)
        {
            if (!CheckBroadcastIsForwardable(broadcastable))
                return;

            // allow other service to handle whatever they need when a broadcast is received and (accepted)
            BroadcasterEventHandler.BroadcastPacketRelayed(broadcastable);

            // try register peer serait géré par une callback sur l'event dans le peer sampling service
            /* TryRegisterPeer(packet.Broadcaster);

             // the sender of a broadcast received by a node should always be in its Callers view 
              to remove TryRegisterPeer(packet.Sender);
 */
            // broadcast cannot be relayed if the node doesn't have any listenners to send it to
            if (context.networkHandling.Listenners.Count <= 0)
                return;

            var count = _fanout > context.networkHandling.Listenners.Count ? context.networkHandling.Listenners.Count : _fanout;

            for (int i = 0; i < count; ++i)
            {
                var count_break = 0;
                var index = 0;
                do
                {
                    index = _random.Next(count);
                    count_break++;

                    if (count_break > count * 2)
                        break;
                }
                // there is a probability that a node receive a broadcast from its callers that has been issued by a node contained in the callers view
                // so we don't want to send it back its message 
                while (context.networkHandling.Listenners.ElementAt(index).Value.peerID == broadcastable.broadcasterID);

                // create a new packet from the received and forwards it to the router
                var relayedPacket = broadcastable.GetForwardablePacket(broadcastable.packet);
                _router.Send(context.networkHandling.Listenners.ElementAt(index).Value.peerAdress, relayedPacket);

                /* _nodeEntity.transportLayer.SendPacketBroadcast(
                                    packet.Broadcaster,
                                    AvalaiblePeers[index],
                                    Protocol.HTTP,
                                    packet.Payload,
                                    // ICI on relaye bien l'identifiant unique du broadcast (ne pas confondre avec l'identifiant unique du message)
                                    // cela permet d'éviter de relayer en boucle un broadcast
                                    packet.BroadcastID);*/
            }
        }

        /// <summary>
        /// Broadcast that are relayed by a node are kept in memory to allow node to limit the number of time they will relay the same broadcast when they receive it.
        /// This buffer will always keeps growing within the node lifetime and has to be clean sometime, depending on a logic that has to be decided
        /// The buffer could have a finite size and message will be replaced as time goes on, taking the oldest entries and considering they are old enough to be forgotten
        /// The cleaning of the buffer can also be a global event broadcasted randomly by a node after a voting process to select a decidor for sending the event ?
        /// </summary>
        public void ClearRelayedBroadcastsBuffer()
        {

        }

        private bool CheckBroadcastIsForwardable(IBroadcastable packet)
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
                if (_relayedBroadcastsBuffer.Count >= _broadcastMessageMaxCycles)
                {
                    _relayedBroadcastsBuffer.Remove(_relayedBroadcastsBuffer.ElementAt(0).Key);
                }
                _relayedBroadcastsBuffer.Add(packet.broadcastID, 0);
                return true;
            }
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
