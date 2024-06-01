using Atom.Components.Gossip;
using Atom.DependencyProvider;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Atom.PlayerSimulation
{
    public class PlayerNetworkSynchronizationComponent : MonoBehaviour, INodeUpdatableComponent, IGossipDataHandler<PlayerPositionsPacket>
    {
        public NodeEntity controller { get; set; }
        [Inject] private GossipComponent _gossipComponent;
        [SerializeField] private PlayerEntity _pf_playerEntity;
        [SerializeField, ReadOnly] private PlayerEntity _playerEntity;

        /// <summary>
        /// Maximum number of unique player datas in each gossip packet
        /// </summary>
        [SerializeField] private int _maxPacketDatasCount = 10;

        /// <summary>
        /// Time between gossip send for position synchronisation
        /// </summary>
        [SerializeField] private float _synchronizationDelay = .25f;

        [SerializeField] private int _synchronisationSlots = 3;

        public PlayerEntity playerEntity => _playerEntity;

        private float _timer;
        private PlayerPositionsPacket _packet;
        private PlayerData _localPlayerData;

        private PlayerData[] _closestDatas = new PlayerData[0];

        public void OnInitialize()
        {

        }

        void Start()
        {
            if (_pf_playerEntity == null)
                return;

            _closestDatas = new PlayerData[_synchronisationSlots];

            _playerEntity = Instantiate(_pf_playerEntity);
            {
                _playerEntity.transform.position = new Vector3(UnityEngine.Random.Range(-50, 50), 0, UnityEngine.Random.Range(-50, 50));
            }
            _playerEntity.Initialize(controller);
            _playerEntity.StartRoaming();


            _localPlayerData = new PlayerData() { PeerAdress = controller.LocalNodeAdress, PeerID = controller.LocalNodeId, WorldPosition = transform.position };
            _gossipComponent.RegisterGossipHandler<PlayerPositionsPacket>(this);
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
                    _gossipComponent.BufferAdd(new PlayerPositionsPacket(_packet.Datas));
                }
                else
                {
                    // the _packet variable holds the previously received datas (filtered/randomly picked and with a max count of 10)
                    _gossipComponent.BufferAdd(new PlayerPositionsPacket(new List<PlayerData>() { _localPlayerData }));
                }

                _timer = 0;
            }
        }


        public void OnReceiveGossip(GossipComponent context, PlayerPositionsPacket data)
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

            // very ugly and iinefficient
            _packet.Datas.Sort((a, b) => Vector3.Distance(WorldSimulationManager.nodeAddresses[a.PeerAdress].Player.transform.position, _playerEntity.transform.position).CompareTo(
                Vector3.Distance(WorldSimulationManager.nodeAddresses[b.PeerAdress].Player.transform.position, _playerEntity.transform.position)));


            for(int i = 0; i < _synchronisationSlots; ++i)
            {
                if (i >= _packet.Datas.Count)
                    break;

                if(_closestDatas[i] == null )
                {
                    _closestDatas[i] = _packet.Datas[i];
                }
                else
                {
                    var dist2 = (WorldSimulationManager.nodeAddresses[_closestDatas[i].PeerAdress].Player.transform.position - _playerEntity.transform.position).magnitude;

                    for (int j = 0; j < _packet.Datas.Count; j++)
                    {
                        var dist = (WorldSimulationManager.nodeAddresses[_packet.Datas[j].PeerAdress].Player.transform.position - _playerEntity.transform.position).magnitude;

                        if (dist < dist2)
                        {
                            _closestDatas[i] = _packet.Datas[i];
                            break;
                        }
                    }
                    
                }
            }

            // select random each time we receive to keep a maximum and reasonable number of datas
            while (_packet.Datas.Count > _maxPacketDatasCount)
            {
                int next = UnityEngine.Random.Range(0, _packet.Datas.Count);
                _packet.Datas.RemoveAt(next);
            }
        }

        void OnDrawGizmos()
        {


            for (int i = 0; i < _closestDatas.Length; ++i)
            {
                if (_closestDatas[i] == null)
                    continue;

                Debug.DrawLine(_playerEntity.transform.position, WorldSimulationManager.nodeAddresses[_closestDatas[i].PeerAdress].Player.transform.position);
            }
        }
    }
}
