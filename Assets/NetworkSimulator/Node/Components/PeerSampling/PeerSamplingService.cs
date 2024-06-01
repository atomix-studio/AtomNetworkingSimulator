using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Atom.Components.Handshaking;
using Atom.Broadcasting;
using Atom.Transport;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Atom.Components.Connecting;
using Atom.ClusterConnectionService;
using Atom.Helpers;

/// <summary>
/// 
/// A r�flechir la possibilit� de broadcast r�guliers � courte port�e-cycle 
/// dans le but de partager des packets de nodes connues � des voisins proches � 1 ou 2 de depth
/// cela pourrait permettre de maintenir un r�seau plus efficace sans attendre la mort des connnections
/// 
/// 
/// 
/// impl�menter un LEASE pour les nodes 
/// 
/// impl�menter un nombre de cycle interne au packet broadcast� poour faire des bc � courte port�e
/// 
/// 
/// </summary>




/// <summary>
/// Responsible of managing the peer network of the local node
/// Subscribing on connection
/// Maintaining (heartbeats)
/// Removing unresponding nodes
/// Ensuring the node is not going isolated
/// </summary>
public class PeerSamplingService : MonoBehaviour, INodeUpdatableComponent
{
    public NodeEntity controller { get; set; }
    [Inject] private TransportLayerComponent _transportLayer;
    [Inject] private NetworkConnectionsComponent _networkInfo;
    [Inject] private BroadcasterComponent _broadcaster;
    [Inject] private HandshakingComponent _handshaking;
    [Inject] private ConnectingComponent _connecting;
    [Inject] private PacketRouter _packetRouter;

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


    private float _discoveryBroadcastCooldown = 0;
    private float _discoveryBroadcastOverrideTimer = 0;

    private void Awake()
    {
        controller = GetComponent<NodeEntity>();
    }

    public void OnInitialize()
    {
        _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(NetworkDiscoveryBroadcastPacket), async (packet) =>
        {

            // when receiving a network discovery broadcast, a node will handle it be either
            // selecting this new peer as a good connection and notify the peer that the connection is accepted
            // forwards the packet to other peers
            var discoveryPacket = (NetworkDiscoveryBroadcastPacket)packet;

            if(discoveryPacket.broadcasterID == _networkInfo.LocalPeerInfo.peerID)
            {
                return;
            }

            // first we check if the packet is coming from a broadcaster that is already a caller
            if (_networkInfo.Connections.TryGetValue(discoveryPacket.broadcasterID, out var _))
            {
                // in this case, we just forward the packet
                _broadcaster.RelayBroadcast((IBroadcastablePacket)packet);
                return;
            }

            // chances to accept a new connection depend on the network size cause the network size will influence directly the number of 
            // discovery request received by a node.
            float chances = NodeMath.Map(WorldSimulationManager.nodeAddresses.Count, 0, 100000, 60, 99.999f);
            var accept_connection = UnityEngine.Random.Range(0, 100) > chances; // here a real random function / use peer counting to get datas of the global network
            if (accept_connection)
            {
                // replace here by an actual ping request to get a score
                var temp_broadcasterPeerInfo = new PeerInfo(discoveryPacket.broadcasterID, discoveryPacket.broadcasterAdress);
                              
                temp_broadcasterPeerInfo.SetScoreByDistance(controller.transform.position);

                if (_networkInfo.Connections.Count >= controller.NetworkViewsTargetCount)
                {
                    //await _networkInfo.UpdatePeerInfoAsync(temp_broadcasterPeerInfo);

                    if (_connecting.CanAcceptConnectionWith(temp_broadcasterPeerInfo, out var swappedPeer))
                    {
                        _connecting.SendConnectionRequestTo(temp_broadcasterPeerInfo, swappedPeer);                        
                    }
                }
                else
                {
                    _connecting.SendConnectionRequestTo(temp_broadcasterPeerInfo);

                    _discoveryBroadcastCooldown = DelayBetweenDiscoveryRequests;
                    return;
                }           
            }

            _broadcaster.RelayBroadcast((IBroadcastablePacket)packet);
        });
    }

    // this method is firing only on first connection to network, as a response from cluster boot nodes
    // boot nodes give to the new peer a bunch of possible peers to connect with
    // the local node will have to ping them to compute a score that represents the value of a connection
    // the highest the cost the most effective the connection will be for the network (we enforce low latency and allowing node with fewer connections to quickly achieve a good number of peers)
    public async void OnReceiveSubscriptionResponse(ClusterConnectionRequestResponsePacket clusterResponse)
    {
        var potentialPeers = clusterResponse.potentialPeerInfos;

        // this should be sending a ping-pong to 
        Task[] handshakeTasks = new Task[potentialPeers.Count];
        for (int i = 0; i < potentialPeers.Count; i++)
        {
            handshakeTasks[i] = _networkInfo.UpdatePeerInfoAsync(potentialPeers[i]);
        }

        await Task.WhenAll(handshakeTasks);

        potentialPeers.Sort((a, b) => a.score.CompareTo(b.score));

        for (int i = 0; i < potentialPeers.Count; i++)
        {
            if (_networkInfo.Connections.Count < controller.NetworkViewsTargetCount)
            {
                // node doesn't have enough connections so it will try each avalaible one
                //TryRegisterPeer(potentialPeers[i]);
                _connecting.SendConnectionRequestTo(potentialPeers[i]);
            }
            else
            {
                for (int j = 0; j < _networkInfo.Connections.Count; ++j)
                {
                    // if better peer is found
                    if (_networkInfo.Connections.ElementAt(j).Value.score < potentialPeers[i].score)
                    {
                        _connecting.DisconnectFromPeer(_networkInfo.Connections.ElementAt(j).Value);
                        _connecting.SendConnectionRequestTo(potentialPeers[i]);
                        break;
                    }
                }
            }
        }

        TryBroadcastDiscoveryRequest();
    }


    public void OnUpdate()
    {
        if (controller.IsSleeping)
            return;
                
        // broadcaster routine is to send requests in the network if its listenners view is not at the target count
        _discoveryBroadcastCooldown -= Time.deltaTime;                    

        if (_networkInfo.Connections.Count < controller.NetworkViewsTargetCount)
        {
            _discoveryBroadcastOverrideTimer += Time.deltaTime;

            TryBroadcastDiscoveryRequest();
        }
    }

    private float GetPeerScore(NodeEntity peer)
    {
        // in a real network situation we would have ping-probed these datas
        if (!peer.IsConnectedAndReady)
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

    /*    // heartbeat should ping random nodes 
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
    */

    [Button]
    private void BroadcastDiscoveryRequestTest()
    {
        TryBroadcastDiscoveryRequest();
    }

    public bool TryBroadcastDiscoveryRequest()
    {
        if (_discoveryBroadcastCooldown > 0 
            && _discoveryBroadcastOverrideTimer < 5) // this timer is incrementend when a node is under its optimal connections count
            return false;

        // the number of broadcasts sent by a node is limited to avoid congestionnning the network
        _discoveryBroadcastCooldown += DelayBetweenDiscoveryRequests;

        _broadcaster.SendBroadcast(new NetworkDiscoveryBroadcastPacket(_networkInfo.LocalPeerInfo.peerAdress));
        _discoveryBroadcastOverrideTimer = 0;
        return true;
    }

    /*[Button]
    // send a request to avalaible peers
    public void SendGroupConnectionRequest()
    {
        for (int i = 0; i < AvalaiblePeers.Count; ++i)
        {
            controller.transportLayer.SendPacket(AvalaiblePeers[i], "GROUP_REQUEST");
        }
    }

    private int _requestIndex = 0;
    public void SendNextGroupConnectionRequest()
    {
        if (AvalaiblePeers.Count == 0)
            return;

        if (_requestIndex >= AvalaiblePeers.Count)
            _requestIndex = 0;

        controller.transportLayer.SendPacket(AvalaiblePeers[_requestIndex], "GROUP_REQUEST");
        _requestIndex++;
    }

    public void SendGroupConnectionBroadcast()
    {
        Broadcast("BROADCAST_GROUP_REQUEST");
    }

    public void OnGroupRequestRefused(NetworkPacket message)
    {
        if (UnityEngine.Random.Range(0, 100) > 100 - ChancesToForgetPeerOnGroupRefused)
            controller.peerSampling.UnregisterPeer(message.Sender);
    }*/
}
