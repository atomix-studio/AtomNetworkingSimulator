using Atom.Components.Gossip;
using Atom.DependencyProvider;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Atom.PlayerSimulation
{
    public class PlayerNetworkSynchronizationComponent : MonoBehaviour, INodeUpdatableComponent, IGossipDataHandler<PlayerInfoPacket>
    {
        public NodeEntity controller { get; set; }
        [Inject] private GossipComponent _gossipComponent;
        [SerializeField] private PlayerEntity _pf_playerEntity;
        [SerializeField, ReadOnly] private PlayerEntity _playerEntity;

        /// <summary>
        /// Maximum number of unique player datas in each gossip packet
        /// </summary>
        [SerializeField] private int _maxPacketPlayerDatasCount = 10;

        /// <summary>
        /// Maximum range a player can be synchronized with another. Above this, the connection will be closed and the data will be kept in the bufferableRange
        /// </summary>
        [SerializeField] private int _synchronizableRange = 15;

        /// <summary>
        /// Maximum number of connections with other peers.
        /// Can be understood as an "instance size" 
        /// </summary>
        [SerializeField] private int _localGroupSize = 8;

        /// <summary>
        /// Time between gossip send for position synchronisation
        /// </summary>
        [SerializeField] private float _synchronizationDelay = .25f;

        public PlayerEntity playerEntity => _playerEntity;

        private float _timer;
        private PlayerInfoPacket _packet;
        private PlayerData _localPlayerData;

        /// <summary>
        /// players currently synchronized with local
        /// </summary>
        private Dictionary<long, PlayerData> _localGroup;

        /// <summary>
        /// Known in range players 
        /// </summary>
        private Dictionary<long, PlayerData> _synchronizablePlayerDatasBuffers;

        public void OnInitialize()
        {

        }

        void Start()
        {
            if (_pf_playerEntity == null)
                return;

            _synchronizablePlayerDatasBuffers = new Dictionary<long, PlayerData>();
            _localGroup = new Dictionary<long, PlayerData>(_localGroupSize);

            _playerEntity = Instantiate(_pf_playerEntity);
            {
                _playerEntity.transform.position = new Vector3(UnityEngine.Random.Range(-150, 150), 0, UnityEngine.Random.Range(-150, 150));
            }
            _playerEntity.Initialize(controller);
            _playerEntity.StartRoaming();


            _localPlayerData = new PlayerData() { PeerAdress = controller.LocalNodeAdress, PeerID = controller.LocalNodeId, WorldPosition = transform.position };
            _gossipComponent.RegisterGossipHandler<PlayerInfoPacket>(this);
            _gossipComponent.RegisterGossipPreUpdateCallback(OnBeforeGossipRound);
        }

        void OnDestroy()
        {
            if (_playerEntity != null)
                Destroy(_playerEntity.gameObject);
        }

        public void OnUpdate()
        {
            if (_playerEntity == null)
                return;

            _timer += Time.deltaTime;
            if (_timer > _synchronizationDelay)
            {
                _localPlayerData.WorldPosition = _playerEntity.transform.position;

                // we have to keep that much of allocations as the reference are kept in the simulation mode without serialization/deserialization
                if (_packet != null)
                {
                    if (!_packet.HasDataForID(_localPlayerData.PeerID))
                        _packet.Datas.Add(_localPlayerData);

                    // the _packet variable holds the previously received datas (filtered/randomly picked and with a max count of 10)
                    _gossipComponent.BufferAdd(new PlayerInfoPacket(_packet.Datas));
                }
                else
                {
                    // the _packet variable holds the previously received datas (filtered/randomly picked and with a max count of 10)
                    _gossipComponent.BufferAdd(new PlayerInfoPacket(new List<PlayerData>() { _localPlayerData }));
                }

                _timer = 0;
            }
        }

        private void OnBeforeGossipRound()
        {
            if (_packet == null)
                return;

            for (int i = 0; i < _packet.Datas.Count; ++i)
            {
                var dist = (_packet.Datas[i].WorldPosition - _playerEntity.transform.position).magnitude;
                // connectable now
                if (dist < _synchronizableRange)
                {
                    if (_localGroup.ContainsKey(_packet.Datas[i].PeerID))
                    {
                        continue;
                    }

                    if (_synchronizablePlayerDatasBuffers.ContainsKey(_packet.Datas[i].PeerID))
                    {
                        continue;
                    }

                    _synchronizablePlayerDatasBuffers.Add(_packet.Datas[i].PeerID, _packet.Datas[i]);
                }              
                
            }

            // select random each time we receive to keep a maximum and reasonable number of datas
            while (_packet.Datas.Count > _maxPacketPlayerDatasCount)
            {
                int next = UnityEngine.Random.Range(0, _packet.Datas.Count);
                _packet.Datas.RemoveAt(next);
            }
        }

        public void OnReceiveGossip(GossipComponent context, PlayerInfoPacket data)
        {
            // if no buffer, we juste take the first incoming and keep the instance
            if (_packet == null)
            {
                _packet = data;
                return;
            }

            // append datas in packet 
            for (int i = 0; i < data.Datas.Count; i++)
            {
                // create or update datas in the buffer
                var pdata = _packet.GetDataForID(data.Datas[i].PeerID);

                if (pdata == null)
                    _packet.Datas.Add(data.Datas[i]);
                else
                {
                    pdata.WorldPosition = data.Datas[i].WorldPosition;
                }
            }
        }


        void OnDrawGizmos()
        {
            foreach(var peer in _synchronizablePlayerDatasBuffers) 
            {              
                Debug.DrawLine(_playerEntity.transform.position, WorldSimulationManager.nodeAddresses[peer.Value.PeerAdress].Player.transform.position);
            }
        }
    }
}
