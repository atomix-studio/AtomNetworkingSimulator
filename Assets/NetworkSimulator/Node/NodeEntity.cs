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
using Atom.DependencyProvider.Samples;
using Atom.Components.GraphNetwork;
using System;

/// <summary>
/// The dependency provider will create an instance for each type inheriting from INodeComponent.
/// This allow the node entity to have all of its components ready at startup, regardless of the fact that the dependencies exists as field of the class.
/// The reason for this is that components may reference one or many other components, and we dont want them to create instances of it internaly. 
/// We actually want all of the components binded to the NodeEntity instance dependencies container.
/// </summary>
[InjectionContext(ForceInheritedTypesInjectionInContext = typeof(INodeComponent))]
public class NodeEntity : MonoBehaviour
{
    public BroadcasterComponent broadcaster { get; set; }
    public TransportLayerComponent transportLayer { get; set; }
    public PeerSamplingService peerSampling { get; set; }
    public NetworkConnectionsComponent networkHandling { get => _networkInfo; set => _networkInfo = value; }
    public GraphEntityComponent graphEntityComponent { get; set; }

    [SerializeField] private NetworkConnectionsComponent _networkInfo;
    [SerializeField] private BootNodeHandling _bootNodeHandling;

    private List<INodeUpdatableComponent> _updatableComponents = new List<INodeUpdatableComponent>();

    [Inject, SerializeField] private WorldSimulationManager _worldSimulationManager;

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
    public long LocalNodeId => networkHandling.LocalPeerInfo.peerID;
    public string LocalNodeAdress => networkHandling.LocalPeerInfo.peerAdress;

    public Material material => _material;

    private float _timerBetweenTryConnection;
    private int _groupRequestsSent;
    private Material _material;

    private void Awake()
    {
        // creating the instances of all components
        DependencyProvider.InjectDependencies(this, null, (dependencies) => {

            foreach (var dependency in dependencies)
            {
                // initializing everyone when everyone is ready
                var nComponent = dependency as INodeComponent;
                if (nComponent != null)
                {
                    nComponent.controller = this;
                    nComponent.OnInitialize();

                    if(nComponent is INodeUpdatableComponent)
                        _updatableComponents.Add(nComponent as INodeUpdatableComponent);
                }
            }
        });
/*
        foreach (var dependency in DependencyProvider.injectionContextContainers[this].InjectedDependencies)
        {
            // initializing everyone when everyone is ready
            var nComponent = dependency.Value as INodeComponent;
            nComponent.context = this;
            nComponent.OnInitialize();
        }*/

        _material = GetComponent<MeshRenderer>().material;

        if (IsBoot)
        {
            _bootNodeHandling = GetNodeComponent<BootNodeHandling>();
        }
    }

    public void OnStart(ClusterInfo clusterInfo, bool sleeping, long id)
    {
        networkHandling.InitializeLocalInfo(new PeerInfo() { peerID = id, peerAdress = this.name, averagePing = 0, trust_coefficient = 0 });

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
        /*var clusterConnectionService = componentProvider.Get<ClusterConnectionService>();
        clusterConnectionService.ConnectToCluster(WorldSimulationManager.defaultCluster);*/
    }

    [Button]

    public async void TestAwaitableGetPingWithPeer(NodeEntity nodeEntity)
    {
        var handshakingService = this.GetDependency<HandshakingComponent>();
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
        if (!_worldSimulationManager.DisplaySelectedOnly || this == _worldSimulationManager.DebugSelectedNodeEntity)
        {

            /*if (WorldSimulationManager.Instance.DisplayCallersConnections)
            {
                for (int i = 0; i < networkHandling.Callers.Count; ++i)
                {
                    Debug.DrawLine(transform.position + Vector3.up, WorldSimulationManager.nodeAddresses[networkHandling.Callers.ElementAt(i).Value.peerAdress].transform.position + Vector3.up, WorldSimulationManager.Instance.DebugSelectedNodeEntity == this ? Color.green : Color.red);
                }
            }*/

            if (_worldSimulationManager.DisplayListennersConnections)
            {
                for (int i = 0; i < networkHandling.Connections.Count; ++i)
                {
                    Debug.DrawLine(transform.position + Vector3.up, WorldSimulationManager.nodeAddresses[networkHandling.Connections.ElementAt(i).Value.peerAdress].transform.position + Vector3.up, _worldSimulationManager.DebugSelectedNodeEntity == this ? Color.green : Color.black);
                }
            }

        }

        if (IsSleeping)
            return;

        CurrentPingSimulatorMultiplier = Mathf.PerlinNoise(UnityEngine.Random.Range(-10, 10), UnityEngine.Random.Range(-10, 10)) * _pingSimulatorStability;

        for (int i = 0; i < _updatableComponents.Count; ++i)
            _updatableComponents[i].OnUpdate();

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
