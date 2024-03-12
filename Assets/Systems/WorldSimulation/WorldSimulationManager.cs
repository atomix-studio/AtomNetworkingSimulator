using Atom.Broadcasting.Consensus;
using Atom.ClusterConnectionService;
using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.EventSystems.EventTrigger;

[Singleton]
public class WorldSimulationManager : MonoBehaviour
{
    public bool DisplayBestConnection;
    public bool DisplayGHSTree;
    public bool DisplayListennersConnections;
    public bool DisplaySelectedOnly;
    public bool DisplayPackets;
    public bool TransportInstantaneously = true;
    public float PacketSpeed = 25;

    [SerializeField] private NodeEntity _pf_NodeEntity;

    [SerializeField] private int _startNodeEntitiesCount = 15;
    [SerializeField] private int _maxNodeEntitiesCount = 100;
    [SerializeField] private int _incomingConnectionsPerMinute = 10;
    [SerializeField] private int _incomingDisconnectionsPerMinute = 7;
    [SerializeField] private bool _autoSpawn = false;

    [SerializeField] ClusterInfo _defaultCluster;

    [SerializeField] private float _spawnTime;
    [SerializeField] private float _spawnTimer;
    [SerializeField] private float _despawnTime;
    [SerializeField] private float _despawnTimer;

    [SerializeField] private List<NodeEntity> _currentAliveNodes = new List<NodeEntity>();

    // as we are simulating the network, the address (IP) is faked and saved in the world manager
    public static Dictionary<string, NodeEntity> nodeAddresses { get; private set; }

    private int _nodeEntityIdGenerator = 0;

    public NodeEntity DebugSelectedNodeEntity;
    public int SelectedIndex;

    public static int _totalPacketSent = 0;
    public static int _totalPacketReceived = 0;
    public static float _startTime;

    public static int _totalPacketSentPerSecondCount = 0;
    public static int _totalPacketReceivedPerSecondCount = 0;
    public static float _packetTimer = 1;

    public Vector2 SpawnSize = new Vector2(-50, 50);

    private void Awake()
    {
        nodeAddresses = new Dictionary<string, NodeEntity>();
    }

    [Button]
    private void ResetCounter()
    {
        _totalPacketSent = 0;
        _totalPacketReceived = 0;
        _startTime = Time.time;
    }

    private void Start()
    {
        for (int i = 0; i < _defaultCluster.BootNodes.Count; i++)
        {
            nodeAddresses.Add(_defaultCluster.BootNodes[i].name, _defaultCluster.BootNodes[i]);
            _defaultCluster.BootNodes[i].networkHandling.InitializeLocalInfo(new PeerInfo() { peerID = i, peerAdress = _defaultCluster.BootNodes[i].name, averagePing = 0, trust_coefficient = 0 });
        }

        GenerateEntities(_startNodeEntitiesCount, false);
    }

    [Button]
    private void GenerateEntities(int nodesCount, bool sleeping) => StartCoroutine(GenerateEntitiesRoutine(nodesCount, sleeping));


    [HorizontalGroup("SHOW"), Button]
    private void ShowPreviousEntity()
    {
        SelectedIndex--;
        if (SelectedIndex < 0)
            SelectedIndex = _currentAliveNodes.Count - 1;

        DebugSelectedNodeEntity = _currentAliveNodes[SelectedIndex];
    }

    [HorizontalGroup("SHOW"), Button]
    private void ShowNextEntity()
    {
        SelectedIndex++;
        if (SelectedIndex > _currentAliveNodes.Count)
            SelectedIndex = 0;

        DebugSelectedNodeEntity = _currentAliveNodes[SelectedIndex];
    }

    private IEnumerator GenerateEntitiesRoutine(int count, bool sleeping = true)
    {
        for (int i = 0; i < count; ++i)
        {
            GenerateNodeEntity(sleeping);
            yield return null;
            yield return null;
        }
    }

    private void Update()
    {
        _packetTimer -= Time.deltaTime;
        if (_packetTimer <= 0)
        {
            _totalPacketSentPerSecondCount = 0;
            _totalPacketReceivedPerSecondCount = 0;
            _packetTimer = 1;
        }

        if (DisplayBestConnection)
        {

            for (int i = 0; i < _currentAliveNodes.Count; ++i)
            {
                var best_con = _currentAliveNodes[i].networkHandling.GetBestConnection();
                if (best_con != null)
                    Debug.DrawLine(_currentAliveNodes[i].transform.position, nodeAddresses[best_con.peerAdress].transform.position, Color.magenta);
            }
        }

        if (DisplayGHSTree)
        {

            for (int i = 0; i < _currentAliveNodes.Count; ++i)
            {
                _currentAliveNodes[i].graphEntityComponent.DisplayDebugConnectionLines();
            }
        }

        if (!_autoSpawn)
            return;

        _spawnTime = 60f / _incomingConnectionsPerMinute;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer > _spawnTime)
        {
            GenerateNodeEntity(false);
            _spawnTimer = 0;
        }

        _despawnTime = 60f / _incomingDisconnectionsPerMinute;
        _despawnTimer += Time.deltaTime;
        if (_despawnTimer > _despawnTime)
        {
            DisconnectRandomNodeEntity();
            _despawnTimer = 0;
        }

    }

    public void GenerateNodeEntity(bool startAsleep)
    {
        var newNodeEntity = PoolManager.Instance.SpawnGo(_pf_NodeEntity.gameObject, transform.position).GetComponent<NodeEntity>();
        if (NavMesh.SamplePosition(new Vector3(Random.Range(SpawnSize.x, SpawnSize.y), 0, Random.Range(SpawnSize.x, SpawnSize.y)), out var hit, 50, 0))
        {
            newNodeEntity.transform.position = hit.position;
        }
        else
        {
            newNodeEntity.transform.position = new Vector3(Random.Range(SpawnSize.x, SpawnSize.y), 0, Random.Range(SpawnSize.x, SpawnSize.y));
        }

        long id = _nodeEntityIdGenerator++;
        newNodeEntity.name = "nodeEntity_" + id;
        _currentAliveNodes.Add(newNodeEntity);
        nodeAddresses.Add(newNodeEntity.name, newNodeEntity);

        newNodeEntity.transform.SetParent(this.transform);
        newNodeEntity.OnStart(_defaultCluster, startAsleep, id);
    }

    public void DisconnectRandomNodeEntity()
    {
        if (_currentAliveNodes.Count == 0)
            return;

        int index = Random.Range(0, _currentAliveNodes.Count);
        PoolManager.Instance.DespawnGo(_currentAliveNodes[index].gameObject);
        _currentAliveNodes.RemoveAt(index);
    }

    [Button]
    private void SleepAll()
    {
        foreach (var node in _currentAliveNodes)
        {
            node.IsSleeping = true;
            node.transportLayer.IsSleeping = true;
            node.GetNodeComponent<PacketRouter>().IsSleeping = true;
        }
    }

    [Button]
    private void AwakeTransportLayerAll()
    {
        foreach (var node in _currentAliveNodes)
        {
            node.transportLayer.IsSleeping = false;
            node.GetNodeComponent<PacketRouter>().IsSleeping = false;
        }
    }

    [Button]
    private void AwakeAll()
    {
        foreach (var node in _currentAliveNodes)
        {
            node.IsSleeping = false;
            node.transportLayer.IsSleeping = false;
            node.GetNodeComponent<PacketRouter>().IsSleeping = false;
        }
    }

    [Button]
    private void VotingTesting()
    {
        foreach (var node in _currentAliveNodes)
        {
            node.material.color = Color.black;
        }

        var startNode = _currentAliveNodes[Random.Range(0, _currentAliveNodes.Count)];
        startNode.GetNodeComponent<ConsensusRequestComponent>().StartColorVoting();
        //startNode.peerSampling.BroadcastBenchmark();
    }

    [Button]
    private void BroadcastTesting()
    {
        foreach (var node in _currentAliveNodes)
        {
            node.material.color = Color.yellow;
        }

        var startNode = _currentAliveNodes[Random.Range(0, _currentAliveNodes.Count)];
        startNode.broadcaster.BroadcastBenchmark();
        //startNode.peerSampling.BroadcastBenchmark();
    }

    [Button]
    private void BroadcastDiscovery()
    {
        var startNode = _currentAliveNodes[Random.Range(0, _currentAliveNodes.Count)];
        startNode.peerSampling.TryBroadcastDiscoveryRequest();
    }

    [Button]
    private void StartGraphcreationSingleCast()
    {
        var startNode = _currentAliveNodes[Random.Range(0, _currentAliveNodes.Count)];
        startNode.graphEntityComponent.StartSpanningTreeCreationWithOneCast();

    }

    [Button]
    private void StartGraphcreationBroadcasted()
    {
        var startNode = _currentAliveNodes[Random.Range(0, _currentAliveNodes.Count)];
        startNode.graphEntityComponent.StartSpanningTreeCreationWithBroadcast();

    }

    [Button]
    private void StopGraphCreation()
    {
        foreach (var node in _currentAliveNodes)
        {
            node.graphEntityComponent.StopSearching();
        }
    }

    [Button]
    private void ResetGraph()
    {
        foreach (var node in _currentAliveNodes)
        {
            node.graphEntityComponent.ResetGraphEdges();
        }
    }

    [Button]
    private void SendGroupConnectionRequest()
    {
        var startNode = _currentAliveNodes[Random.Range(0, _currentAliveNodes.Count)];
        startNode.peerSampling.SendGroupConnectionRequest();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Total packet received : " + _totalPacketReceived);
        EditorGUILayout.LabelField("Average packet received / s : " + (_totalPacketReceived / (Time.time - _startTime)));
        EditorGUILayout.LabelField("Current packet received / s : " + _totalPacketReceivedPerSecondCount);
        EditorGUILayout.LabelField("Total packet sent : " + _totalPacketSent);
        EditorGUILayout.LabelField("Current packet sent / s : " + _totalPacketSentPerSecondCount);
        EditorGUILayout.LabelField("Average packet sent / s : " + (_totalPacketSent / (Time.time - _startTime)));
    }
}
