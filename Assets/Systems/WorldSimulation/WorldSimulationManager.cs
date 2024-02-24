using Atom.ClusterConnectionService;
using Atom.CommunicationSystem;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class WorldSimulationManager : MonoBehaviour
{
    public static WorldSimulationManager Instance;

    public bool DisplayGroupConnections;
    public bool DisplayListennersConnections;
    public bool DisplaySelectedOnly;
    [OnValueChanged("updateDisplayPackets")] public bool DisplayPackets;

    [OnValueChanged("updateTransportInstantaneously")]
    public bool TransportInstantaneously = true;
    public static bool transportInstantaneously = false;
    private void updateTransportInstantaneously() => transportInstantaneously = Instance.TransportInstantaneously;

    private void updateDisplayPackets() => displayPackets = Instance.DisplayPackets;
    public static bool displayPackets = true;

    [OnValueChanged("updatePacketSpeed")] public float PacketSpeed = 100;
    private void updatePacketSpeed() => packetSpeed = Instance.PacketSpeed; 

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
    public static float packetSpeed { get; private set; } = 75;
    public static ClusterInfo defaultCluster => Instance._defaultCluster;

    private int _nodeEntityIdGenerator = 0;

    public NodeEntity DebugSelectedNodeEntity;
    public int SelectedIndex;

    public static int _totalPacketSent = 0;
    public static int _totalPacketReceived = 0;
    public static float _startTime;

    public static int _totalPacketSentPerSecondCount = 0;
    public static int _totalPacketReceivedPerSecondCount = 0;
    public static float _packetTimer = 1;


    private void Awake()
    {
        // cheap singleton for prototyping is goodenough
        Instance = this;
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
        for (int i = 0; i < defaultCluster.BootNodes.Count; i++)
        {
            nodeAddresses.Add(defaultCluster.BootNodes[i].name, defaultCluster.BootNodes[i]);
            defaultCluster.BootNodes[i].networkHandling.InitializeLocalInfo(new PeerInfo() { peerID = defaultCluster.BootNodes[i].name, peerAdress = defaultCluster.BootNodes[i].name, ping = 0, trust_coefficient = 0 });
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
        if (NavMesh.SamplePosition(new Vector3(Random.Range(-50, 50), 0, Random.Range(-50, 50)), out var hit, 50, 0))
        {
            newNodeEntity.transform.position = hit.position;
        }
        else
        {
            newNodeEntity.transform.position = new Vector3(Random.Range(-50, 50), 0, Random.Range(-50, 50));
        }

        newNodeEntity.name = "nodeEntity_" + _nodeEntityIdGenerator++;
        _currentAliveNodes.Add(newNodeEntity);
        nodeAddresses.Add(newNodeEntity.name, newNodeEntity);
      
        newNodeEntity.transform.SetParent(this.transform);
        newNodeEntity.OnStart(_defaultCluster, startAsleep);
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
    private void SetAllSleeping(bool entity, bool transportlayer)
    {
        foreach (var node in _currentAliveNodes)
        {
            if (entity)
                node.IsSleeping = true;

            if (transportlayer)
            {
                node.transportLayer.IsSleeping = true;
                node.GetNodeComponent<PacketRouter>().IsSleeping = true;
            }
        }
    }

    [Button]
    private void SetAllAwaken(bool entity, bool transportlayer)
    {
        foreach (var node in _currentAliveNodes)
        {
            if (entity)
                node.IsSleeping = false;

            if (transportlayer)
            {
                node.transportLayer.IsSleeping = false;
                node.GetNodeComponent<PacketRouter>().IsSleeping = false;
            }
        }
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
        startNode.peerSampling.BroadcastDiscoveryRequest();
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
