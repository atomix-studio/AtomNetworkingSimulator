using Atom.Components.Gossip;
using Atom.DependencyProvider;
using Atom.Simulation.Player;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Atom.PlayerSimulation
{
    public class PlayerNetworkSynchronizationComponent : MonoBehaviour, INodeUpdatableComponent, IGossipDataHandler<PlayerPositionsPacket>
    {
        public NodeEntity controller { get; set; }
        [Inject] private GossipComponent _gossipComponent;
        [SerializeField, ReadOnly] private PlayerEntity _playerEntity;

        /// <summary>
        /// Maximum number of unique player datas in each gossip packet
        /// </summary>
        [SerializeField] private int _maxPacketDatasCount = 10;

        /// <summary>
        /// Time between gossip send for position synchronisation
        /// </summary>
        [SerializeField] private float _synchronizationDelay = .25f;

        private float _timer;
        private PlayerPositionsPacket _packet;
        private PlayerData _localPlayerData;

        public void OnInitialize()
        {
            _playerEntity = Instantiate(_playerEntity);
            if( NavMesh.SamplePosition(new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100)), out var navMeshHit, 10, 0))
            {
                _playerEntity.transform.position = navMeshHit.position;
                _playerEntity.StartRoaming();
            }
            _localPlayerData = new PlayerData() { PeerAdress = controller.LocalNodeAdress, PeerID = controller.LocalNodeId, WorldPosition = transform.position };
            _gossipComponent.RegisterGossipHandler<PlayerPositionsPacket>(this);
        }

        public void OnUpdate()
        {
            _timer += Time.deltaTime;
            if(_timer > _synchronizationDelay)
            {
                _localPlayerData.WorldPosition = transform.position;

                // we have to keep that much of allocations as the reference are kept in the simulation mode without serialization/deserialization
                if (_packet != null)
                {                    
                    if(!_packet.HasDataForID(_localPlayerData.PeerID))
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

        public void OnReceiveGossip(PlayerPositionsPacket data)
        {
            // if no buffer, we juste take the first incoming and keep the instance
            if(_packet == null)
            {
                _packet = data;
                return;
            }

            // append datas in packet 
            for(int i = 0; i < data.Datas.Count; i++)
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

            // select random each time we receive to keep a maximum and reasonable number of datas
            while(_packet.Datas.Count > _maxPacketDatasCount)
            {
                int next = UnityEngine.Random.Range(0, _packet.Datas.Count);
                _packet.Datas.RemoveAt(next);
            }
        }
    }
}
