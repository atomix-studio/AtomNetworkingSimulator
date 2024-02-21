using Atom.Transport;
using Atom.CommunicationSystem;
using Atom.ComponentSystem;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Atom.ClusterConnectionService;
using Atom.Services.Handshaking;

public class NodeEntity : MonoBehaviour
{
    public NodeComponentProvider componentProvider { get; private set; }

    public BroadcasterComponent broadcaster { get; private set; }
    public NetworkInfoComponent networkInfo { get; private set; }
    public TransportLayerComponent transportLayer { get; private set; }


    [Header("Params")]
    [SerializeField] private bool _isBoot;
    [SerializeField] private int _preferedGroupSize = 8;
    [SerializeField] private float _delayBetweenGroupFindingRequest = 1;

    // the highest value  the more the ping will do up and down with high amplitude
    [SerializeField] private float _pingSimulatorStability = 1;

    [Header("Variables")]
    public bool IsConnected = false;
    public bool IsInGroup = false;
    public bool IsSleeping = false;

    public float CurrentPingSimulatorMultiplier = 1;

    public bool IsBoot => _isBoot;
    public Material material => _material;

    /// <summary>
    /// The current group entities, alive TCP connections
    /// </summary>
    public List<NodeEntity> Connections = new List<NodeEntity>();

    public PeerSamplingService peerDiscoveryComponent;

    private float _timerBetweenTryConnection;
    private int _groupRequestsSent;
    private Material _material;

    private void Awake()
    {
        componentProvider = GetComponent<NodeComponentProvider>();
        componentProvider.Initialize();

        broadcaster = (BroadcasterComponent)componentProvider.Get<BroadcasterComponent>();
        transportLayer = (TransportLayerComponent)componentProvider.Get<TransportLayerComponent>();
        networkInfo = (NetworkInfoComponent)componentProvider.Get<NetworkInfoComponent>();
        networkInfo.Initialize(new PeerInfo() { peerAdress = this.name, ping = 0, trust_coefficient = 0 });     


        peerDiscoveryComponent = GetComponent<PeerSamplingService>();
        _material = GetComponent<MeshRenderer>().material;
    }

    public T GetNodeComponent<T>() where T: INodeComponent
    {
        return (T)componentProvider.Get<T>();
    }

    void OnDisable()
    {
        for (int i = 0; i < Connections.Count; ++i)
        {
            Connections[i]?.Disconnect(this);
        }

        Connections.Clear();
        IsConnected = false;
        IsInGroup = false;
        _material.color = Color.gray;
    }

    [Button]
    public void TestConnectToCluster()
    {
        var clusterConnectionService = (ClusterConnectionService)componentProvider.Get<ClusterConnectionService>();
        clusterConnectionService.ConnectToCluster(WorldSimulationManager.defaultCluster);
    }

    [Button]

    public async void TestAwaitableGetPingWithPeer(NodeEntity nodeEntity)
    {
        var handshakingService = (HandshakingComponent)componentProvider.Get<HandshakingComponent>();
        var ping = await handshakingService.GetPingWithPeerTask(nodeEntity);
        Debug.LogError("Ping" + ping);
    }


    private bool IsConnectedWith(NodeEntity requester)
    {
        for (int i = 0; i < Connections.Count; ++i)
        {
            if (Connections[i] == requester) return true;
        }
        return false;
    }

    // DIFFERENCIER REJOINDRE GROUPE DU REQUESTER ET GROUPE DU LOCAL 
    public void OnReceiveGroupRequest(NodeEntity requester)
    {
        if (Connections.Count < _preferedGroupSize
            && !IsConnectedWith(requester))
        {
            //   requester.JoinLocalGroup();

            if (requester.Connect(this))
            {
                Connections.Add(requester);
                IsInGroup = true;
            }
        }
        else
        {
            transportLayer.SendPacket(requester, "GROUP_REQUEST_REFUSED");
        }
    }


    public bool Connect(NodeEntity connection)
    {
        if (Connections.Count < _preferedGroupSize
             && !IsConnectedWith(connection))
        {
            Debug.Log($"Connection from {connection} accepter by {this}");
            Connections.Add(connection);

            if (connection.IsInGroup && !IsInGroup)
            {
                _material.color = connection.material.color;
            }
            else if (!connection.IsInGroup && IsInGroup)
            {
                connection.material.color = _material.color;
            }
            else if (!connection.IsInGroup && !IsInGroup)
            {
                Color color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1);
                connection.material.color = color;
                _material.color = color;
            }

            return true;
        }

        return false;
    }

    public void Disconnect(NodeEntity connection)
    {
        //Debug.Log($"{this} is disconnecting from {connection}");
        Connections.Remove(connection);
    }

    private void Update()
    {
        if(!WorldSimulationManager.Instance.DisplaySelectedOnly || this == WorldSimulationManager.Instance.DebugSelectedNodeEntity)
        {
            if (WorldSimulationManager.Instance.DisplayGroupConnections)
            {
                for (int i = 0; i < Connections.Count; ++i)
                {
                    Debug.DrawLine(transform.position + Vector3.up, Connections[i].transform.position + Vector3.up, WorldSimulationManager.Instance.DebugSelectedNodeEntity == this ? Color.green : Color.red);
                }
            }
            else if (WorldSimulationManager.Instance.DisplayPartialViewPeers)
            {
                for (int i = 0; i < peerDiscoveryComponent.AvalaiblePeers.Count; ++i)
                {
                    Debug.DrawLine(transform.position + Vector3.up, peerDiscoveryComponent.AvalaiblePeers[i].transform.position + Vector3.up, WorldSimulationManager.Instance.DebugSelectedNodeEntity == this ? Color.green : Color.red);
                }
            }
        }

        if (IsSleeping)
            return;

        CurrentPingSimulatorMultiplier = Mathf.PerlinNoise(Random.Range(-10, 10), Random.Range(-10, 10)) * _pingSimulatorStability;

        peerDiscoveryComponent.OnUpdated();

        if (_isBoot)
            return;

        if (Connections.Count < _preferedGroupSize)
        {
            _timerBetweenTryConnection += Time.deltaTime;
            if (_timerBetweenTryConnection > _delayBetweenGroupFindingRequest)
            {
                // broadcast in the network to find new connections (TCP)
                peerDiscoveryComponent.SendNextGroupConnectionRequest();
                _timerBetweenTryConnection = 0;
            }
        }

        IsInGroup = Connections.Count > 0;
    }
}
