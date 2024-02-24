using Atom.CommunicationSystem;
using Atom.Components.Handshaking;
using Atom.ComponentProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.Connecting
{
    /// <summary>
    /// Component responsible of mutual join in network between two nodes
    /// The requester become the caller and the responder become the listenner
    /// </summary>
    public class ConnectingComponent : INodeComponent
    {
        public NodeEntity context { get; set; }
        [InjectComponent] private PacketRouter _packetRouter;
        [InjectComponent] private PeerSamplingService _peerSampling;
        [InjectComponent] private NetworkHandlingComponent _networkInfo;
        [InjectComponent] private HandshakingComponent _handshaking;

        public void OnInitialize()
        {
            _packetRouter.RegisterPacketHandler(typeof(ConnectionRequestResponsePacket), null);
            _packetRouter.RegisterPacketHandler(typeof(ConnectionRequestPacket), (received) =>
            {
                var respondable = (IRespondable)received;
                var response = (ConnectionRequestResponsePacket)respondable.GetResponsePacket(respondable);
                var connectionRequest = (ConnectionRequestPacket)received;

                // is this node avalaible ?
                // multiplying ping per 2 in this case to simulate the return as we dont want to ping the node now
                var ping = (DateTime.Now - received.sentTime).Milliseconds * 2f;
                PeerInfo peerInfo = new PeerInfo(received.senderID, respondable.senderAdress);
                peerInfo.ComputeScore(ping, connectionRequest.networkInfoCallersCount, connectionRequest.networkInfoListennersCount);

                response.isAccepted = TryAcceptCaller(peerInfo);

                _packetRouter.SendResponse((IRespondable)received, response);
            });

            _packetRouter.RegisterPacketHandler(typeof(DisconnectFromPeerNotificationPacket), (packet) =>
            {
                var adress = (packet as IRespondable).senderAdress;
                var caller = _networkInfo.FindCallerByAdress(adress);
                if (caller != null) 
                {
                    _networkInfo.RemoveCaller(caller);
                    return;
                }

                var listenner = _networkInfo.FindListennerByAdress(adress);
                if(listenner != null)
                {
                    _networkInfo.RemoveListenner(listenner);
                }
            });
        }

        /// <summary>
        /// A connection request aims to add the requested node in LISTENNERS view
        /// </summary>
        /// <param name="peerInfo"></param>
        public void SendConnectionRequestTo(PeerInfo peerInfo)
        {
            var sentTime = DateTime.Now;
            _packetRouter.SendRequest(peerInfo.peerAdress, new ConnectionRequestPacket((byte)_networkInfo.Callers.Count, (byte)_networkInfo.Listenners.Count), (response) =>
            {
                var connectionResponsePacket = (ConnectionRequestResponsePacket)response;
                peerInfo.ping = (DateTime.Now - sentTime).Milliseconds;
                // we want to be sure that the sure is up to date here because if its 0 the new connection could be replaced by a worst one at any time
                peerInfo.ComputeScore(peerInfo.ping, context.NetworkViewsTargetCount, context.NetworkViewsTargetCount);
                if (connectionResponsePacket.isAccepted)
                {
                    _networkInfo.AddListenner(peerInfo);
                }
            });
        }

        public bool TryAcceptCaller(PeerInfo peerInfo)
        {
            if (_networkInfo.Callers.Count == 0)
            {
                _networkInfo.AddCaller(peerInfo);
                return true;
            }

            if (_networkInfo.Callers.Count >= context.NetworkViewsTargetCount)
            {
                // trying to replace an existing worst caller by the requesting one
                foreach (var caller in _networkInfo.Callers)
                {
                    if (peerInfo.score > caller.Value.score)
                    {
                        // add random function
                        // replacing the listenner by the new peer
                        DisconnectFromCaller(caller.Value);

                        _networkInfo.AddCaller(peerInfo);

                        return true;
                    }
                }

                return false;
            }

            _networkInfo.AddCaller(peerInfo);
            return true;
        }

        public bool TryAcceptListenner(PeerInfo peerInfo)
        {
            if (_networkInfo.Listenners.Count == 0)
            {
                _networkInfo.AddListenner(peerInfo);
                return true;
            }

            if (_networkInfo.Listenners.Count >= context.NetworkViewsTargetCount)
            {
                // trying to replace an existing worst caller by the requesting one
                foreach (var listenner in _networkInfo.Listenners)
                {
                    if (peerInfo.score > listenner.Value.score)
                    {
                        // add random function
                        // replacing the listenner by the new peer

                        DisconnectFromListenner(listenner.Value);

                        _networkInfo.AddListenner(peerInfo);

                        return true;
                    }
                }

                return false;
            }

            _networkInfo.AddListenner(peerInfo);
            return true;
        }

        public bool CanAcceptListenner(PeerInfo peerInfo)
        {
            if (_networkInfo.Listenners.Count == 0)
            {
                return true;
            }

            if (_networkInfo.Listenners.Count >= context.NetworkViewsTargetCount)
            {
                // trying to replace an existing worst caller by the requesting one
                foreach (var listenner in _networkInfo.Listenners)
                {
                    if (peerInfo.score > listenner.Value.score)
                    {                        
                        return true;
                    }
                }

                return false;
            }

            return true;
        }

        public void DisconnectFromCaller(PeerInfo peerInfo)
        {
            _networkInfo.RemoveCaller(peerInfo);
            _packetRouter.Send(peerInfo.peerAdress, new DisconnectFromPeerNotificationPacket());
        }

        public void DisconnectFromListenner(PeerInfo peerInfo)
        {
            _networkInfo.RemoveListenner(peerInfo);
            _packetRouter.Send(peerInfo.peerAdress, new DisconnectFromPeerNotificationPacket());
        }

    }
}
