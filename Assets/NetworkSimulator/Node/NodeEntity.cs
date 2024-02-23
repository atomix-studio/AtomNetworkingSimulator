using Atom.Transport;
using Atom.CommunicationSystem;
using Atom.ComponentProvider;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Atom.ClusterConnectionService;
using Atom.Components.Handshaking;
using System.Linq;

public class NodeEntity : MonoBehaviour
{
    public NodeComponentProvider componentProvider { get; private set; }
    public BroadcasterComponent broadcaster { get; private set; }
    public TransportLayerComponent transportLayer { get; private set; }
    public NetworkHandlingComponent networkHandling { get => _networkInfo; private set => _networkInfo = value; }
    public PeerSamplingService peerSampling { get; private set; }

    [SerializeField] private NetworkHandlingComponent _networkInfo;

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


    private float _timerBetweenTryConnection;
    private int _groupRequestsSent;
    private Material _material;

    private void Awake()
    {
        componentProvider = GetComponent<NodeComponentProvider>();
        componentProvider.Initialize();

        broadcaster = GetNodeComponent<BroadcasterComponent>();
        transportLayer = GetNodeComponent<TransportLayerComponent>();
        networkHandling = GetNodeComponent<NetworkHandlingComponent>();
        peerSampling = GetNodeComponent<PeerSamplingService>();

        _material = GetComponent<MeshRenderer>().material;
    }

    public void OnStart(ClusterInfo clusterInfo, bool sleeping)
    {
        networkHandling.InitializeLocalInfo(new PeerInfo() { peerAdress = this.name, ping = 0, trust_coefficient = 0 });

        GetNodeComponent<ClusterConnectionService>().ConnectToCluster(clusterInfo);
        IsSleeping = sleeping;
    }

    public T GetNodeComponent<T>() where T: INodeComponent
    {
        return componentProvider.Get<T>();
    }

    void OnDisable()
    {
        for (int i = 0; i < Connections.Count; ++i)
        {
            Connections[i]?.GroupDisconnect(this);
        }

        Connections.Clear();
        IsConnected = false;
        IsInGroup = false;
        _material.color = Color.gray;
    }

    [Button]
    public void TestConnectToCluster()
    {
        var clusterConnectionService = componentProvider.Get<ClusterConnectionService>();
        clusterConnectionService.ConnectToCluster(WorldSimulationManager.defaultCluster);
    }

    [Button]

    public async void TestAwaitableGetPingWithPeer(NodeEntity nodeEntity)
    {
        var handshakingService = componentProvider.Get<HandshakingComponent>();
        var ping = await handshakingService.GetPingAsync(nodeEntity);
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

            if (requester.GroupConnect(this))
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


    public bool GroupConnect(NodeEntity connection)
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

    public void GroupDisconnect(NodeEntity connection)
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
            else
            {
                if (WorldSimulationManager.Instance.DisplayCallersConnections)
                {
                    for (int i = 0; i < networkHandling.Callers.Count; ++i)
                    {
                        Debug.DrawLine(transform.position + Vector3.up, WorldSimulationManager.nodeAddresses[networkHandling.Callers.ElementAt(i).Value.peerAdress].transform.position + Vector3.up, WorldSimulationManager.Instance.DebugSelectedNodeEntity == this ? Color.green : Color.red);
                    }
                }

                if (WorldSimulationManager.Instance.DisplayListennersConnections)
                {
                    for (int i = 0; i < networkHandling.Listenners.Count; ++i)
                    {
                        Debug.DrawLine(transform.position + Vector3.up, WorldSimulationManager.nodeAddresses[networkHandling.Listenners.ElementAt(i).Value.peerAdress].transform.position + Vector3.up, WorldSimulationManager.Instance.DebugSelectedNodeEntity == this ? Color.green : Color.green);
                    }
                }
            }
        }

        if (IsSleeping)
            return;

        CurrentPingSimulatorMultiplier = Mathf.PerlinNoise(Random.Range(-10, 10), Random.Range(-10, 10)) * _pingSimulatorStability;

        //peerSampling.OnUpdated();

        if (_isBoot)
            return;

        if (Connections.Count < _preferedGroupSize)
        {
            _timerBetweenTryConnection += Time.deltaTime;
            if (_timerBetweenTryConnection > _delayBetweenGroupFindingRequest)
            {
                // broadcast in the network to find new connections (TCP)
                peerSampling.SendNextGroupConnectionRequest();
                _timerBetweenTryConnection = 0;
            }
        }

        IsInGroup = Connections.Count > 0;
    }
}
