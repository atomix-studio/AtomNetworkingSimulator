using Atom.CommunicationSystem;
using Atom.Components.Handshaking;
using Atom.ComponentProvider;
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
                var connectionRequest = (ConnectionRequestPacket)received;

                // is this node avalaible ?
                // multiplying ping per 2 in this case to simulate the return as we dont want to ping the node now
                var ping = (DateTime.Now - received.sentTime).Milliseconds * 2f;
                PeerInfo peerInfo = new PeerInfo(received.senderID, respondable.senderAdress);
                peerInfo.ComputeScore(ping, connectionRequest.senderConnectionsCount);

                peerInfo.SetScoreByDistance(context.transform.position);

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

            _packetRouter.SendRequest(peerInfo.peerAdress, new ConnectionRequestPacket((byte)_networkInfo.Connections.Count), (response) =>
            {
                if (response == null)
                    //tcs.SetResult(false);
                    return;

                var connectionResponsePacket = (ConnectionRequestResponsePacket)response;
                if (connectionResponsePacket.isAccepted)
                {
                    peerInfo.ping = 2 * (DateTime.Now - response.sentTime).Milliseconds;
                    // we want to be sure that the sure is up to date here because if its 0 the new connection could be replaced by a worst one at any time
                    peerInfo.ComputeScore(peerInfo.ping, context.NetworkViewsTargetCount);
                    peerInfo.SetScoreByDistance(context.transform.position);
                    _networkInfo.AddConnection(peerInfo);


                    if (removeOnSuccss != null)
                        DisconnectFromPeer(removeOnSuccss);
                }

                //tcs.SetResult(connectionResponsePacket.isAccepted);
            });

            //return await tcs.Task;
        }

        /*        public bool TryAcceptCaller(PeerInfo peerInfo)
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
        */

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

            if (_networkInfo.Connections.Count >= context.NetworkViewsTargetCount)
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
