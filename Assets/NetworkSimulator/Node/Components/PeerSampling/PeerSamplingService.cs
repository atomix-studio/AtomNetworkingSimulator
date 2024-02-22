using Atom.CommunicationSystem;
using Atom.ComponentSystem;
using Atom.Components.Handshaking;
using Atom.Transport;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Atom.Components.Connecting;

/// <summary>
/// 
/// A réflechir la possibilité de broadcast réguliers à courte portée-cycle 
/// dans le but de partager des packets de nodes connues à des voisins proches à 1 ou 2 de depth
/// cela pourrait permettre de maintenir un réseau plus efficace sans attendre la mort des connnections
/// 
/// 
/// 
/// implémenter un LEASE pour les nodes 
/// 
/// implémenter un nombre de cycle interne au packet broadcasté poour faire des bc à courte portée
/// 
/// 
/// </summary>




[NodeComponentInjector]
/// <summary>
/// Responsible of managing the peer network of the local node
/// Subscribing on connection
/// Maintaining (heartbeats)
/// Removing unresponding nodes
/// Ensuring the node is not going isolated
/// </summary>
public class PeerSamplingService : MonoBehaviour, INodeUpdatableComponent
{
    public NodeEntity context { get; set; }
    [InjectNodeComponentDependency] private TransportLayerComponent _transportLayer;
    [InjectNodeComponentDependency] private NetworkHandlingComponent _networkInfo;
    [InjectNodeComponentDependency] private BroadcasterComponent _broadcaster;
    [InjectNodeComponentDependency] private HandshakingComponent _handshaking;
    [InjectNodeComponentDependency] private ConnectingComponent _connecting;

    public int ListennersTargetCount = 9;

    /// <summary>
    /// Known connections that can be 
    /// </summary>
    public List<NodeEntity> AvalaiblePeers = new List<NodeEntity>();

    /// <summary>
    /// the broadcaster will try to keep 
    /// </summary>
    public int PartialViewMaximumCount = 9;
    public int RefreshingTime = 10;

    // each time a node relays a discovery, it will increment a timer to avoid too frequent broadcasting discoveries over the network
    public int DelayBetweenDiscoveryRequests = 10;
    public int Fanout = 3;
    public int BroadcastForwardingMaxCycles = 2;

    public int ChancesToForgetPeerOnGroupRefused = 10;
    private Dictionary<int, int> _relayedBroadcasts = new Dictionary<int, int>();
    public Dictionary<int, int> relayedBroadcasts => _relayedBroadcasts;


    private float _discoveryBroadcastTimer = 0;
    private float _refreshTimer = 0;

    private float _peersScore = 1;

    private System.Random random = new System.Random((int)DateTime.Now.Ticks % int.MaxValue);

    private void Awake()
    {
        context = GetComponent<NodeEntity>();
    }

    public void OnInitialize()
    {
        _broadcaster.RegisterPacketHandler(typeof(NetworkDiscoveryBroadcastPacket), (packet) =>
        {
        
        });

        _broadcaster.RegisterPacketHandler(typeof(NetworkDiscoveryBroadcastResponsePacket), (packet) =>
        {
        
        });
    }

    private void Start()
    {
        context.transportLayer.RegisterEndpoint("BROADCAST_GROUP_REQUEST", (packet) =>
        {
            context.OnReceiveGroupRequest(packet.Broadcaster);
            RelayBroadcast(packet);
        });

 /*       context.transportLayer.RegisterEndpoint("NEW_SUBSCRIPTION_REQUEST", (subscriberPacket) =>
        {
            _transportLayer.SendPacket(subscriberPacket.Sender, "NEW_SUBSCRIPTION_REQUEST_RESPONSE", AvalaiblePeers);
        });

        context.transportLayer.RegisterEndpoint("NEW_SUBSCRIPTION_REQUEST_RESPONSE", (packetResponse) =>
        {
            var potentialPeers = packetResponse._potentialPeers;
            potentialPeers.Sort((a, b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
            for (int i = 0; i < potentialPeers.Count; i++)
            {
                if (AvalaiblePeers.Count < PartialViewMaximumCount)
                {
                    TryRegisterPeer(potentialPeers[i]);
                }
                else
                {
                    for (int j = 0; j < AvalaiblePeers.Count; ++j)
                    {
                        // if better peer is found
                        if (GetPeerScore(AvalaiblePeers[j]) < GetPeerScore(potentialPeers[i]))
                        {
                            UnregisterPeer(AvalaiblePeers[j]);
                            TryRegisterPeer(potentialPeers[i]);
                            break;
                        }
                    }
                }
            }

            BroadcastDiscoveryRequest();
        });
*/
        context.transportLayer.RegisterEndpoint("BROADCAST_DISCOVERY", OnReceive_DiscoveryBroadcast);
        context.transportLayer.RegisterEndpoint("BROADCAST_DISCOVERY_RESPONSE", OnReceive_DiscoveryBroadcastReponse);
    }

    // this method is firing only on first connection to network, as a response from cluster boot nodes
    // boot nodes give to the new peer a bunch of possible peers to connect with
    // the local node will have to ping them to compute a score that represents the value of a connection
    // the highest the cost the most effective the connection will be for the network (we enforce low latency and allowing node with fewer connections to quickly achieve a good number of peers)
    public async void OnReceiveSubscriptionResponse(SubscriptionResponsePacket subscriptionResponsePacket)
    {
        var potentialPeers = subscriptionResponsePacket.potentialPeerInfos;

        // this should be sending a ping-pong to 
        Task[] handshakeTasks = new Task[potentialPeers.Count];
        for (int i = 0; i < potentialPeers.Count; i++)
        {

        }

        for (int i = 0; i < potentialPeers.Count; i++)
        {
            if (_networkInfo.Listenners.Count < ListennersTargetCount)
            {
                // node doesn't have enough connections so it will try each avalaible one
                //TryRegisterPeer(potentialPeers[i]);
                _connecting.SendConnectionRequestTo(potentialPeers[i]);
            }
            else
            {
                for (int j = 0; j < _networkInfo.Listenners.Count; ++j)
                {
                    // if better peer is found
                    if (_networkInfo.Listenners.ElementAt(j).Value.score < potentialPeers[i].score)
                    {
                        _connecting.DisconnectFrom(_networkInfo.Listenners.ElementAt(j).Value);
                        _connecting.SendConnectionRequestTo(potentialPeers[i]);
                        break;
                    }
                }
            }
        }

        BroadcastDiscoveryRequest();
    }

    public void OnUpdate()
    {
        if (context.IsSleeping)
            return;

        // done by the networkHandlingComponent
        /*_refreshTimer += Time.deltaTime;
        if (_refreshTimer > RefreshingTime)
        {
            Heartbeat();
            _refreshTimer = 0;
        }*/

        _discoveryBroadcastTimer -= Time.deltaTime;
        if (_discoveryBroadcastTimer > 0)
            return;

        if (_networkInfo.Listenners.Count < ListennersTargetCount)
        {
            BroadcastDiscoveryRequest();
        }
    }

    private float GetPeerScore(NodeEntity peer)
    {
        // in a real network situation we would have ping-probed these datas
        if (!peer.IsConnected)
            return 0;

        if (!peer.gameObject.activeSelf)
            return 0;

        var dist = Vector3.Distance(peer.transform.position, transform.position);
        return 1f / dist * 100f;
    }

    public void TryRegisterPeer(NodeEntity nodeEntity)
    {
        if (AvalaiblePeers.Count >= PartialViewMaximumCount)
        {
            // we put this check here because its more cpu expensive to do a contains rather than a count comparison
            if (AvalaiblePeers.Contains(nodeEntity))
                return;

            for (int j = 0; j < AvalaiblePeers.Count; ++j)
            {
                if (GetPeerScore(nodeEntity) > GetPeerScore(AvalaiblePeers[j]))
                {
                    UnregisterPeer(AvalaiblePeers[j]);
                    AvalaiblePeers.Add(nodeEntity);
                    break;
                }
            }
            return;

        }

        //Debug.Log($"{this} registers new peer : {nodeEntity}");

        if (!AvalaiblePeers.Contains(nodeEntity))
            AvalaiblePeers.Add(nodeEntity);
    }

    public void UnregisterPeer(NodeEntity nodeEntity)
    {
        AvalaiblePeers.Remove(nodeEntity);
    }

    // heartbeat should ping random nodes 
    // these nodes then now that local is alive
    // if ping has reponse, the local know that pinged is alive
    private void Heartbeat()
    {
        for (int i = 0; i < AvalaiblePeers.Count; ++i)
        {
            if (!AvalaiblePeers[i].gameObject.activeSelf)
            {
                //Debug.LogError("Removing unavalaible peer");
                AvalaiblePeers.RemoveAt(i);
                i--;
            }
        }

        if (AvalaiblePeers.Count == 0)
            return;

        // here we will try to check the connectivity health of the local node
        // we do so by computing a peer score value (indexex on the latency to each distant peer and maybe other things like message loss, etc..)
        // if the computed score is lower enough from a previous value, we will trigger the call of a discovery request

        var current_peers_score = 0f;
        // the heartbeat should be able to maintain a score routine and see if a peer becomes too low in PeerScore (like latency is increasing)
        for (int i = 0; i < AvalaiblePeers.Count; ++i)
        {
            current_peers_score += GetPeerScore(AvalaiblePeers[i]);
        }
        current_peers_score /= AvalaiblePeers.Count;

        // abritrary value for now
        // the idea is to check if the score is decreasing from a previous high value
        // if so, the node will trigger a discovery request to recreate its network with better peers
        if (current_peers_score * 1.33f < _peersScore)
        {
            BroadcastDiscoveryRequest();
            _peersScore = current_peers_score;
        }
        else if (current_peers_score > _peersScore)
        {
            _peersScore = current_peers_score;
        }
    }

    [Button]
    public void BroadcastDiscoveryRequest()
    {
        if(_networkInfo.Listenners.Count == 0)
            return;

        _discoveryBroadcastTimer += DelayBetweenDiscoveryRequests;

        var count = Fanout > AvalaiblePeers.Count ? AvalaiblePeers.Count : Fanout;

        for (int i = 0; i < count; ++i)
        {
            var index = random.Next(AvalaiblePeers.Count);
            context.transportLayer.SendPacketBroadcast(context, AvalaiblePeers[index], "BROADCAST_DISCOVERY");
        }
    }

    // les méthodes du broadcasting
    // SendBroadcast
    // RelayBroadcast
    // CallbackBroadcast

    // les paramètres
    // maximum de cycles
    // % de chance de ne pas relayer
    // timeout
    // dont send already relayed = base

    public void OnReceive_DiscoveryBroadcast(NetworkPacket packet)
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


        TryRegisterPeer(packet.Broadcaster);
        // récupérer les infos "au passage" lors d'un broadcast permet d'alimenter plus rapidement les connections connues 
        // limitant ainsi la nécessité pour elle d'envoyer des broadcast de découverte réseau
        TryRegisterPeer(packet.Sender);

        if (AvalaiblePeers.Count <= 0)
            return;

        _discoveryBroadcastTimer = DelayBetweenDiscoveryRequests;

        // in this case we answer the broadcaster only if the local node decided this peer was a better option and keeps it data in avalaible peers
        if (AvalaiblePeers.Contains(packet.Broadcaster))
            context.transportLayer.SendPacket(packet.Broadcaster, "BROADCAST_DISCOVERY_RESPONSE");

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

            context.transportLayer.SendPacketBroadcast(
                               packet.Broadcaster,
                               AvalaiblePeers[index],
                               "BROADCAST_DISCOVERY",
                               // ICI on relaye bien l'identifiant unique du broadcast (ne pas confondre avec l'identifiant unique du message)
                               // cela permet d'éviter de relayer en boucle un broadcast
                               packet.BroadcastID);
        }
    }

    [Button]
    public void BroadcastBenchmark()
    {
        if (AvalaiblePeers.Count <= 0)
            return;

        var count = Fanout > AvalaiblePeers.Count ? AvalaiblePeers.Count : Fanout;

        for (int i = 0; i < count; ++i)
        {
            var index = random.Next(AvalaiblePeers.Count);
            context.transportLayer.SendPacketBroadcast(context, AvalaiblePeers[index], "BROADCAST_BENCHMARK");
        }
    }

    public void OnReceive_BenchmarkBroadcast(NetworkPacket packet)
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

        TryRegisterPeer(packet.Broadcaster);
        // récupérer les infos "au passage" lors d'un broadcast permet d'alimenter plus rapidement les connections connues 
        // limitant ainsi la nécessité pour elle d'envoyer des broadcast de découverte réseau
        TryRegisterPeer(packet.Sender);

        // callback to broadcaster
        //_nodeEntity.transportLayer.SendPacket(packet.Broadcaster, Protocol.HTTP, "BROADCAST_DISCOVERY_RESPONSE");

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

                if (count_break > AvalaiblePeers.Count)
                    break;
            }
            while (AvalaiblePeers[index] == packet.Broadcaster
                  || AvalaiblePeers[index] == packet.Sender);

            context.transportLayer.SendPacketBroadcast(
               packet.Broadcaster,
               AvalaiblePeers[index],
               packet.Payload,
               // ICI on relaye bien l'identifiant unique du broadcast (ne pas confondre avec l'identifiant unique du message)
               // cela permet d'éviter de relayer en boucle un broadcast
               packet.BroadcastID);
        }
    }

    public void OnReceive_DiscoveryBroadcastReponse(NetworkPacket packet)
    {
        TryRegisterPeer(packet.Sender);
    }

    [Button]
    // send a request to avalaible peers
    public void SendGroupConnectionRequest()
    {
        for (int i = 0; i < AvalaiblePeers.Count; ++i)
        {
            context.transportLayer.SendPacket(AvalaiblePeers[i], "GROUP_REQUEST");
        }
    }

    private int _requestIndex = 0;
    public void SendNextGroupConnectionRequest()
    {
        if (AvalaiblePeers.Count == 0)
            return;

        if (_requestIndex >= AvalaiblePeers.Count)
            _requestIndex = 0;

        context.transportLayer.SendPacket(AvalaiblePeers[_requestIndex], "GROUP_REQUEST");
        _requestIndex++;
    }

    public void SendGroupConnectionBroadcast()
    {
        Broadcast("BROADCAST_GROUP_REQUEST");
    }

    public void OnGroupRequestRefused(NetworkPacket message)
    {
        if (UnityEngine.Random.Range(0, 100) > 100 - ChancesToForgetPeerOnGroupRefused)
            context.peerSampling.UnregisterPeer(message.Sender);
    }

    public void Broadcast(string PAYLOAD_NAME)
    {
        if (AvalaiblePeers.Count <= 0)
            return;

        var count = Fanout > AvalaiblePeers.Count ? AvalaiblePeers.Count : Fanout;
        for (int i = 0; i < count; ++i)
        {
            var index = random.Next(AvalaiblePeers.Count);
            context.transportLayer.SendPacketBroadcast(context, AvalaiblePeers[index], PAYLOAD_NAME);
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

            context.transportLayer.SendPacketBroadcast(
                               packet.Broadcaster,
                               AvalaiblePeers[index],
                               packet.Payload,
                               // ICI on relaye bien l'identifiant unique du broadcast (ne pas confondre avec l'identifiant unique du message)
                               // cela permet d'éviter de relayer en boucle un broadcast
                               packet.BroadcastID);
        }

    }

}
