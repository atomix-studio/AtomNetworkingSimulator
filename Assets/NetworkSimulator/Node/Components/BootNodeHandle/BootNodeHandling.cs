using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Atom.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.Connecting
{
    [Serializable]
    public class BootNodeHandling : INodeUpdatableComponent
    {
        public NodeEntity controller { get; set; }
        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private NetworkConnectionsComponent _networkHandling;

        protected float broadcastChances = 3;
        protected float refreshCooldown = 5;
        private float _middlewareCooldown = 0;

        public void OnInitialize()
        {
            if (!controller.IsBoot)
                return;

            _broadcaster.RegisterBroadcastReceptionMiddleware(BootNodeBroadcastMiddleware);
        }

        // boot node have a special routine that will randomly refresh their known nodes when they relay a broadcast
        private bool BootNodeBroadcastMiddleware(IBroadcastablePacket packet)
        {
            if (_middlewareCooldown > 0)
                return true;

            if(NodeRandom.Range(0, 100) < 100 - broadcastChances) 
                return true;

            if (!_networkHandling.KnownPeers.ContainsKey(packet.broadcasterID)
                && !_networkHandling.Connections.ContainsKey(packet.broadcasterID))
            {
                if(_networkHandling.KnownPeers.Count >= _networkHandling.KnownPeersMaximumCount)
                {
                    int randomIndex = NodeRandom.Range(0, _networkHandling.KnownPeersMaximumCount);
                    _networkHandling.KnownPeers.Remove(_networkHandling.KnownPeers.ElementAt(randomIndex).Key);
                }
                
                // ping the peer via broadcast ?

                _middlewareCooldown = refreshCooldown;
            }

            return true;
        }

        public void OnUpdate()
        {
            if (_middlewareCooldown > 0)
                _middlewareCooldown -= Time.deltaTime;
            else
                _middlewareCooldown = 0;
        }
    }
}
