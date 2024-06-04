using Atom.CommunicationSystem;
using Atom.Components.Gossip;
using Atom.Components.RpcSystem;
using Atom.DependencyProvider;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Atom.PlayerSimulation
{
    public class PlayerNetworkSynchronizationComponent : MonoBehaviour, INodeUpdatableComponent, IGossipDataHandler<PlayerInfoPacket>
    {
        public NodeEntity controller { get; set; }
        [Inject] private GossipComponent _gossipComponent;
        [Inject] private PacketRouter _packetRouter;

        [SerializeField] private PlayerEntity _pf_playerEntity;
        [SerializeField, ReadOnly] private PlayerEntity _playerEntity;

        /// <summary>
        /// Maximum number of unique player datas in each gossip packet
        /// </summary>
        [SerializeField] private int _maxPacketPlayerDatasCount = 8;

        /// <summary>
        /// Maximum range a player can be synchronized with another. Above this, the connection will be closed and the data will be kept in the bufferableRange
        /// </summary>
        [SerializeField] private int _synchronizableRange = 16;

        /// <summary>
        /// 
        /// </summary>
        [SerializeField] private int _localGroupSize = 16;

        /// <summary>
        /// Maximum number of connections with other peers.
        /// Can be understood as an "instance size" 
        /// </summary>
        [SerializeField] private int _synchronizationBufferSize = 64;

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
        [ShowInInspector] private Dictionary<long, PlayerData> _localGroup;

        [ShowInInspector] private HashSet<long> _pendingRequests;

        /// <summary>
        /// Known in range players 
        /// </summary>
        [ShowInInspector] private Dictionary<long, PlayerData> _synchronizablePlayerDatasBuffers;

        public void OnInitialize()
        {
            _packetRouter.RegisterPacketHandler<SyncRequestPacket>(OnReceiveSynchronizationRequest);
            _packetRouter.RegisterPacketHandler<SyncResponsePacket>(null);
            _packetRouter.RegisterPacketHandler<DesyncNotificationPacket>(OnReceiveDesynchronizationNotification);
        }

        void Start()
        {
            if (_pf_playerEntity == null)
                return;

            _synchronizablePlayerDatasBuffers = new Dictionary<long, PlayerData>(_synchronizationBufferSize);
            _localGroup = new Dictionary<long, PlayerData>(_localGroupSize);
            _pendingRequests = new HashSet<long>(_localGroupSize);

            _playerEntity = Instantiate(_pf_playerEntity);
            {
                _playerEntity.transform.position = new Vector3(UnityEngine.Random.Range(-75, 75), 0, UnityEngine.Random.Range(-75, 75));
            }
            _playerEntity.Initialize(controller);
            _playerEntity.StartRoaming();


            _localPlayerData = new PlayerData() { PeerAdress = controller.LocalNodeAdress, PeerID = controller.LocalNodeId, WorldPosition = transform.position };
            _gossipComponent.RegisterGossipHandler<PlayerInfoPacket>(this);
            _gossipComponent.RegisterGossipPreUpdateCallback(OnBeforeGossipRound);
        }

        void OnDisable()
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
                SendGossip();

                UpdateSynchronization();

                _timer = 0;
            }
        }

        private void SendGossip()
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
                    if (_synchronizablePlayerDatasBuffers.Count >= _synchronizationBufferSize)
                        continue;

                    if (_synchronizablePlayerDatasBuffers.ContainsKey(_packet.Datas[i].PeerID))
                        continue;

                    _synchronizablePlayerDatasBuffers.Add(_packet.Datas[i].PeerID, _packet.Datas[i]);
                }
                else
                {
                    if (_synchronizablePlayerDatasBuffers.ContainsKey(_packet.Datas[i].PeerID))
                    {
                        _synchronizablePlayerDatasBuffers.Remove(_packet.Datas[i].PeerID);
                    }
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
                var found = false;

                for (int j = 0; j < _packet.Datas.Count; ++j)
                {
                    if (_packet.Datas[j].PeerID == data.Datas[i].PeerID)
                    {
                        _packet.Datas[j] = data.Datas[i];
                        found = true;
                        break;
                    }
                }

                if (found)
                    continue;

                _packet.Datas.Add(data.Datas[i]);
            }
        }

        private void UpdateSynchronization()
        {
            // ugly allocatiion, to refact
            var vals = _localGroup.Values.ToList();
            for (int i = 0; i < vals.Count; ++i)
            {
                var dist = (vals[i].WorldPosition - _playerEntity.transform.position).magnitude;
                if (dist > _synchronizableRange)
                {
                    // disconnect
                    _localGroup.Remove(vals[i].PeerID);
                    SendPlayerDesynchronizationNotification(vals[i]);
                }
            }

            // to do compare if synchronizable are very closer from localGroup to swap connection


            // ********

            // simply do nothin if local group (aka synchronizations with local node) is filled
            if (_localGroup.Count >= _localGroupSize)
                return;

            if (_synchronizablePlayerDatasBuffers.Count <= 0)
                return;

            for (int i = _localGroup.Count; i < _localGroupSize; ++i)
            {
                var next = UnityEngine.Random.Range(0, _synchronizablePlayerDatasBuffers.Count);
                var peer = _synchronizablePlayerDatasBuffers.ElementAt(next);

                //if already requesting, dont send 
                if (_pendingRequests.Contains(peer.Key))
                {
                    continue;
                }

                // we keep a ref of synced player also in the syncBuffer to avoid 
                if (_localGroup.ContainsKey(peer.Key))
                    continue;

                SendPlayerSynchronizationRequest(peer.Value);
            }
        }

        public class SyncRequestPacket : AbstractNetworkPacket, IRespondable
        {
            public string senderAdress { get; set; }

            public INetworkPacket packet => this;

            public IResponse GetResponsePacket(IRespondable answerPacket)
            {
                return new SyncResponsePacket();
            }
        }

        public class SyncResponsePacket : AbstractNetworkPacket, IResponse
        {
            public long callerPacketUniqueId { get; set; }

            public INetworkPacket packet => this;

            public int requestPing { get; set; }
        }

        public struct DesyncNotificationPacket : INetworkPacket
        {
            public short packetTypeIdentifier { get ; set ; }
            public long packetUniqueId { get ; set; }
            public long senderID { get; set; }
            public DateTime sentTime { get; set; }

            public void DisposePacket()
            {
                //throw new NotImplementedException();
            }
        }

        public async void SendPlayerSynchronizationRequest(PlayerData target)
        {
            if (_pendingRequests.Contains(target.PeerID))
                return;

            _pendingRequests.Add(target.PeerID);

            _packetRouter.SendRequest<SyncResponsePacket>(target.PeerAdress, new SyncRequestPacket(), (responsePacket) =>
            {
                if (responsePacket == null)
                {
                    _synchronizablePlayerDatasBuffers.Remove(target.PeerID);
                }
                else
                {
                    if (!_localGroup.ContainsKey(target.PeerID))
                        _localGroup.Add(target.PeerID, target);
                }

                _pendingRequests.Remove(target.PeerID);

            }, 2000);
        }

        public async void SendPlayerDesynchronizationNotification(PlayerData target)
        {
            _packetRouter.Send(target.PeerAdress, new DesyncNotificationPacket());
        }

        private void OnReceiveSynchronizationRequest(SyncRequestPacket requestPacket)
        {
            if (_localGroup.Count >= _localGroupSize)
                return;

            if (_localGroup.ContainsKey(requestPacket.senderID))
                return;

            if (_synchronizablePlayerDatasBuffers.TryGetValue(requestPacket.senderID, out var playerData))
            {
                _localGroup.Add(requestPacket.senderID, playerData);
                _packetRouter.SendResponse(requestPacket, new SyncResponsePacket());
            }
        }

        private void OnReceiveDesynchronizationNotification(DesyncNotificationPacket desyncNotificationPacket)
        {
            if (_localGroup.ContainsKey(desyncNotificationPacket.senderID))
            {
                _localGroup.Remove(desyncNotificationPacket.senderID);
            }
            else
            {
                Debug.Log($"{desyncNotificationPacket.senderID} wasn't in localGroup");
            }
        }

        void OnDrawGizmos()
        {
            if (_synchronizablePlayerDatasBuffers == null)
                return;

            foreach (var peer in _localGroup)
            {
                if (!WorldSimulationManager.nodeAddresses.ContainsKey(peer.Value.PeerAdress))
                {
                    // disconnected
                    continue;
                }

                if (WorldSimulationManager.nodeAddresses[peer.Value.PeerAdress].Player == null)
                    continue;

                Debug.DrawLine(_playerEntity.transform.position, WorldSimulationManager.nodeAddresses[peer.Value.PeerAdress].Player.transform.position);
            }

            if(Selection.activeGameObject == _playerEntity.gameObject)
            {

                foreach (var peer in _localGroup)
                {
                    // If the other also 'sees' this player
                    if (WorldSimulationManager.nodeAddresses[peer.Value.PeerAdress].playerNetworkSynchronization._localGroup.ContainsKey(controller.LocalNodeId))
                    {
                        Gizmos.color = Color.green;
                    }
                    else
                    {
                        Gizmos.color = Color.red;
                    }

                    Gizmos.DrawSphere(WorldSimulationManager.nodeAddresses[peer.Value.PeerAdress].Player.transform.position, 1.5f);
                }

                Gizmos.color = Color.white;

            }
        }
    }
}
