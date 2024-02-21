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
        [InjectNodeComponentDependency] private NetworkInfoComponent _networkInfo;
        [InjectNodeComponentDependency] private HandshakingComponent _handshaking;

        public void OnInitialize()
        {
            _packetRouter.RegisterPacketHandler(typeof(ConnectionRequestPacket), (received) =>
            {
                var respondable = (IRespondable)received;
                var response = (ConnectionRequestResponsePacket)respondable.GetResponsePacket(respondable);
                var connectionRequest = (ConnectionRequestPacket)received;

                // is this node avalaible ?
                // multiplying ping per 2 in this case to simulate the return as we dont want to ping the node now
                var ping = (DateTime.Now - received.sentTime).Milliseconds * 2f;
                PeerInfo peerInfo = new PeerInfo(received.senderID, respondable.senderAdress);
                peerInfo.UpdatePeerScore(ping, connectionRequest.networkInfoCallersCount, connectionRequest.networkInfoListennersCount);

                response.isAccepted = AcceptConnection(peerInfo);

                if (response.isAccepted)
                {
                    Debug.Log($"{this} adding new caller => {peerInfo.peerAdress}");
                    _networkInfo.Callers.Add(peerInfo.peerAdress, peerInfo);
                }

                _packetRouter.SendResponse((IRespondable)received, response);
            });
        }

        public void RequestConnectionTo(PeerInfo peerInfo)
        {

        }

        public bool AcceptConnection(PeerInfo peerInfo)
        {
            if (_networkInfo.Listenners.Count == 0)
                return true;

            if(_networkInfo.Listenners.Count >= _peerSampling.ListennersTargetCount)
            {
                // trying to replace an existing worst listenner by the requesting one
                foreach (var listenner in _networkInfo.Listenners)
                {                   
                    if (peerInfo.score > listenner.Value.score)
                    {
                        // add random function
                        // replacing the listenner by the new peer
                        DisconnectFrom(listenner.Value);

                        return true;
                    }
                }
            }

            return false;
        }

    }
}
