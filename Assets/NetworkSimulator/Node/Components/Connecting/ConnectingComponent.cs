using Atom.CommunicationSystem;
using Atom.Components.Handshaking;
using Atom.ComponentSystem;
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
        [InjectNodeComponentDependency] private PacketRouter _packetRouter;
        [InjectNodeComponentDependency] private PeerSamplingService _peerSampling;
        [InjectNodeComponentDependency] private NetworkHandlingComponent _networkInfo;
        [InjectNodeComponentDependency] private HandshakingComponent _handshaking;

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

                response.isAccepted = AcceptConnection(peerInfo);

                _packetRouter.SendResponse((IRespondable)received, response);
            });

            _packetRouter.RegisterPacketHandler(typeof(DisconnectFromPeerNotificationPacket), (packet) =>
            {
                if (_networkInfo.Callers.TryGetValue((packet as IRespondable).senderAdress, out var peerInfo))
                {
                    _networkInfo.RemoveCaller(peerInfo);
                }
            });
        }

        public void SendConnectionRequestTo(PeerInfo peerInfo)
        {
            _packetRouter.SendRequest(peerInfo.peerAdress, new ConnectionRequestPacket((byte)_networkInfo.Callers.Count, (byte)_networkInfo.Listenners.Count), (response) =>
            {
                var connectionResponsePacket = (ConnectionRequestResponsePacket)response;
                if (connectionResponsePacket.isAccepted)
                {
                    _networkInfo.AddListenner(peerInfo);
                }
            });
        }

        public bool AcceptConnection(PeerInfo peerInfo)
        {
            if (_networkInfo.Callers.Count == 0)
            {
                _networkInfo.AddCaller(peerInfo);
                return true;
            }

            if (_networkInfo.Callers.Count >= _peerSampling.ListennersTargetCount)
            {
                // trying to replace an existing worst listenner by the requesting one
                foreach (var listenner in _networkInfo.Callers)
                {
                    if (peerInfo.score > listenner.Value.score)
                    {
                        // add random function
                        // replacing the listenner by the new peer
                        DisconnectFrom(listenner.Value);
                        _networkInfo.AddCaller(peerInfo);

                        return true;
                    }
                }
            }

            return false;
        }

        public void DisconnectFrom(PeerInfo peerInfo)
        {
            _networkInfo.RemoveCaller(peerInfo);
            _packetRouter.Send(peerInfo.peerAdress, new DisconnectFromPeerNotificationPacket());
        }

    }
}
