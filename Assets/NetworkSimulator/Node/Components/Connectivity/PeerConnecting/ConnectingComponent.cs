using Atom.CommunicationSystem;
using Atom.Components.Handshaking;
using Atom.DependencyProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Atom.Helpers;

namespace Atom.Components.Connecting
{
    /// <summary>
    /// Component responsible of mutual join in network between two nodes
    /// The requester become the caller and the responder become the listenner
    /// </summary>
    public class ConnectingComponent : INodeComponent
    {
        public NodeEntity controller { get; set; }
        [Inject] private PacketRouter _packetRouter;
        [Inject] private PeerNetworkDiscoveryComponent _peerSampling;
        [Inject] private NetworkConnectionsComponent _networkInfo;
        [Inject] private HandshakingComponent _handshaking;

        public void OnInitialize()
        {
            _packetRouter.RegisterPacketHandler(typeof(ConnectionRequestResponsePacket), null);
            _packetRouter.RegisterPacketHandler(typeof(ConnectionRequestPacket), (received) =>
            {
                var respondable = (IRespondable)received;
                var connectionRequest = (ConnectionRequestPacket)received;

                // is this node avalaible ?
                // multiplying ping per 2 in this case to simulate the return as we dont want to ping the node now
                PeerInfo peerInfo = new PeerInfo(received.senderID, respondable.senderAdress);
                peerInfo.SetScoreByDistance(controller.transform.position);
                peerInfo.UpdateAveragePing(2 * received.GetReceptionDelayMs());

                if (CanAcceptConnectionWith(peerInfo, out var toRemove))
                {
                    var response = (ConnectionRequestResponsePacket)respondable.GetResponsePacket(respondable);

                    response.isAccepted = true;
                    _networkInfo.AddConnection(peerInfo);
                    _packetRouter.SendResponse((IRespondable)received, response);

                    if(toRemove != null)
                        DisconnectFromPeer(toRemove);   
                }
            });

            _packetRouter.RegisterPacketHandler(typeof(DisconnectFromPeerNotificationPacket), (packet) =>
            {
                var adress = (packet as DisconnectFromPeerNotificationPacket).senderAdress;
                /* var caller = _networkInfo.FindCallerByAdress(adress);
                 if (caller != null) 
                 {
                     _networkInfo.RemoveCaller(caller);
                     return;
                 }*/

                var connection = _networkInfo.FindConnectionByAdress(adress);
                if (connection != null)
                {
                    _networkInfo.RemoveConnection(connection);
                }
            });
        }

        /// <summary>
        /// A connection request aims to add the requested node in LISTENNERS view
        /// </summary>
        /// <param name="peerInfo"></param>
        public void SendConnectionRequestTo(PeerInfo peerInfo, PeerInfo removeOnSuccss = null)
        {
            //var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (_networkInfo.Connections.ContainsKey(peerInfo.peerID))
                return;

           /* peerInfo.pendingConnectionResponse = true;
            _networkInfo.Connections.Add(peerInfo.peerID, peerInfo);*/

            _packetRouter.SendRequest(peerInfo.peerAdress, new ConnectionRequestPacket((byte)_networkInfo.Connections.Count), (response) =>
            {
                if (response == null)
                {
                   /* _networkInfo.Connections.Remove(peerInfo.peerID);*/
                    peerInfo.Dispose();
                    return;
                }
                //tcs.SetResult(false);

                var connectionResponsePacket = (ConnectionRequestResponsePacket)response;
                if (connectionResponsePacket.isAccepted)
                {

                    peerInfo.UpdateAveragePing(2 * response.GetReceptionDelayMs());
                    // we want to be sure that the sure is up to date here because if its 0 the new connection could be replaced by a worst one at any time
                    peerInfo.SetScoreByDistance(controller.transform.position);
                    peerInfo.UpdateAveragePing(connectionResponsePacket.requestPing);

                    peerInfo.requestedByLocal = true;
                    _networkInfo.AddConnection(peerInfo);

                    if (removeOnSuccss != null)
                        DisconnectFromPeer(removeOnSuccss);
                }
                else
                {
                    // for the sake of network traffic, connection request that aren't accepted are not sent. 
                    // the requester will handle that code in the timeout (response == null)
                    // in case of a misusage/modification of that logic later, I let this here
                    //_networkInfo.Connections.Remove(peerInfo.peerID);
                    peerInfo.Dispose();
                }
                //tcs.SetResult(connectionResponsePacket.isAccepted);
            });

            //return await tcs.Task;
        }
               
        /// <summary>
        /// Is there any room for this new peer OR this new peers fits better (better score) ?
        /// </summary>
        /// <param name="peerInfo"></param>
        /// <returns></returns>
        public bool CanAcceptConnectionWith(PeerInfo peerInfo, out PeerInfo toDisconnect)
        {
            toDisconnect = null;

            if (peerInfo.peerAdress == _networkInfo.LocalPeerInfo.peerAdress)
                return false;

            if (_networkInfo.Connections.ContainsKey(peerInfo.peerID))
                return false;

            if (_networkInfo.Connections.Count == 0)
            {
                return true;
            }

            if (_networkInfo.Connections.Count >= controller.NetworkViewsTargetCount)
            {
                // trying to replace an existing worst caller by the requesting one
                foreach (var connection in _networkInfo.Connections)
                {
                    if (peerInfo.score > connection.Value.score)
                    {
                        toDisconnect = connection.Value;
                        return true;
                    }

                   /* if (NodeRandom.Range(0f, 100f) > 97)
                    {
                        toDisconnect = connection.Value;
                        return true;
                    }*/
                }

                return false;
            }

            return true;
        }


        /*
                public void DisconnectFromCaller(PeerInfo peerInfo)
                {
                    _networkInfo.RemoveCaller(peerInfo);
                    _packetRouter.Send(peerInfo.peerAdress, new DisconnectFromPeerNotificationPacket());
                }
        */
        public void DisconnectFromPeer(PeerInfo peerInfo)
        {
            _networkInfo.RemoveConnection(peerInfo);
            _packetRouter.Send(peerInfo.peerAdress, new DisconnectFromPeerNotificationPacket());
        }

    }
}
