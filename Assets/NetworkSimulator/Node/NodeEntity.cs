using Atom.Transport;
using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Atom.Broadcasting;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Atom.ClusterConnectionService;
using Atom.Components.Handshaking;
using System.Linq;
using Atom.Components.Connecting;
using Sirenix.Utilities;
using UnityEditor.Rendering.LookDev;

[InjectionContext(ForceInheritedTypesInjectionInContext = typeof(INodeComponent))]
public class NodeEntity : MonoBehaviour
{
    public NodeComponentProvider componentProvider { get; private set; }
    public BroadcasterComponent broadcaster { get; set; }
    public TransportLayerComponent transportLayer { get; set; }
    public PeerSamplingService peerSampling { get; set; }
    public NetworkHandlingComponent networkHandling { get => _networkInfo; set => _networkInfo = value; }

    [SerializeField] private NetworkHandlingComponent _networkInfo;
    private BootNodeHandling _bootNodeHandling;

    [Header("Params")]
    [SerializeField] private bool _isBoot;
    [SerializeField] private int _preferedGroupSize = 8;
    [SerializeField] private float _delayBetweenGroupFindingRequest = 1;

    // the highest value  the more the ping will do up and down with high amplitude
    [SerializeField] private float _pingSimulatorStability = 1;

    // aimed number of callers/listenners
    [SerializeField] private int _networkViewsTargetCount = 9;

    public int NetworkViewsTargetCount => _networkViewsTargetCount;

    [Header("Variables")]
    public bool IsConnectedAndReady = false;
    public bool IsInGroup = false;
    public bool IsSleeping = false;

    public float CurrentPingSimulatorMultiplier = 1;

    public bool IsBoot => _isBoot;
    public Material material => _material;

    private float _timerBetweenTryConnection;
    private int _groupRequestsSent;
    private Material _material;

    private void Awake()
    {
        // creating the instances of all components
        DependencyProvider.injectDependencies(this);

        foreach (var dependency in DependencyProvider.injectionContextContainers[this].InjectedDependencies)
        {
            // injecting dependencies in components from this context container as they are shared
            // components with take references to instances created by the injection of dependencies in the nodeEntity
            DependencyProvider.injectDependencies(dependency.Value, this);

            // ***************
            // WIP should be done automatically as the context is an interface property marked witj InjectComponent
            (dependency.Value as INodeComponent).context = this;
        }
        foreach (var dependency in DependencyProvider.injectionContextContainers[this].InjectedDependencies)
        {
            // initializing everyone when everyone is ready
            (dependency.Value as INodeComponent).OnInitialize();
        }

        /*        componentProvider = GetComponent<NodeComponentProvider>();
                componentProvider.Initialize();
        *//*
        broadcaster = GetNodeComponent<BroadcasterComponent>();
        transportLayer = GetNodeComponent<TransportLayerComponent>();
        networkHandling = GetNodeComponent<NetworkHandlingComponent>();
        peerSampling = GetNodeComponent<PeerSamplingService>();
*/
        _material = GetComponent<MeshRenderer>().material;

        if (IsBoot)
        {
            _bootNodeHandling = GetNodeComponent<BootNodeHandling>();
        }
    }

    public void OnStart(ClusterInfo clusterInfo, bool sleeping)
    {
        networkHandling.InitializeLocalInfo(new PeerInfo() { peerID = System.Guid.NewGuid().ToString(), peerAdress = this.name, averagePing = 0, trust_coefficient = 0 });

        GetNodeComponent<ClusterConnectionService>().ConnectToCluster(clusterInfo);
        IsSleeping = sleeping;
    }

    public T GetNodeComponent<T>() where T : INodeComponent
    {
        return (T)DependencyProvider.getOrCreate(typeof(T), this);
    }

    void OnDisable()
    {

        IsConnectedAndReady = false;
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

    // DIFFERENCIER REJOINDRE GROUPE DU REQUESTER ET GROUPE DU LOCAL 
    public void OnReceiveGroupRequest(NodeEntity requester)
    {
        /* if (Connections.Count < _preferedGroupSize
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
         }*/
    }


    public bool GroupConnect(NodeEntity connection)
    {
        /*if (Connections.Count < _preferedGroupSize
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
        }*/

        return false;
    }

    private void Update()
    {
        if (!WorldSimulationManager.Instance.DisplaySelectedOnly || this == WorldSimulationManager.Instance.DebugSelectedNodeEntity)
        {

            /*if (WorldSimulationManager.Instance.DisplayCallersConnections)
            {
                for (int i = 0; i < networkHandling.Callers.Count; ++i)
                {
                    Debug.DrawLine(transform.position + Vector3.up, WorldSimulationManager.nodeAddresses[networkHandling.Callers.ElementAt(i).Value.peerAdress].transform.position + Vector3.up, WorldSimulationManager.Instance.DebugSelectedNodeEntity == this ? Color.green : Color.red);
                }
            }*/

            if (WorldSimulationManager.Instance.DisplayListennersConnections)
            {
                for (int i = 0; i < networkHandling.Connections.Count; ++i)
                {
                    Debug.DrawLine(transform.position + Vector3.up, WorldSimulationManager.nodeAddresses[networkHandling.Connections.ElementAt(i).Value.peerAdress].transform.position + Vector3.up, WorldSimulationManager.Instance.DebugSelectedNodeEntity == this ? Color.green : Color.black);
                }
            }

        }

        if (IsSleeping)
            return;

        CurrentPingSimulatorMultiplier = Mathf.PerlinNoise(Random.Range(-10, 10), Random.Range(-10, 10)) * _pingSimulatorStability;

        //peerSampling.OnUpdated();

        if (_isBoot)
            return;
        /*
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

                IsInGroup = Connections.Count > 0;*/
    }

}
