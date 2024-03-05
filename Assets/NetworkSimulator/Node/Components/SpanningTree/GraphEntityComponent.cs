using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.GraphNetwork
{
    [Serializable]
    public class GraphFragmentData
    {
        public string FragmentOwnerId { get; set; } = string.Empty; // PEER ID OF THE FRAGMENT LEAD
        public short FragmentLevel { get; set; } = 0;

        public List<PeerInfo> FragmentMembers { get; set; } = new List<PeerInfo>();

        public PeerInfo MinimumOutgoingEdgePeer { get; set; } = null;

        public GraphFragmentData(short fragmentLevel, string fragmentLeaderId)
        {
            FragmentLevel = fragmentLevel;
            FragmentOwnerId = fragmentLeaderId;
        }

    }


    public class GraphEntityComponent : MonoBehaviour, INodeComponent
    {
        public NodeEntity controller { get; set; }

        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private PacketRouter _packetRouter;
        [Inject] private NetworkHandlingComponent _networkHandling;

        [Header("GraphEntityComponent")]
        [SerializeField] private GraphFragmentData _fragmentData;

        public bool ConnectionRequestSent = false;
        public string PendingFragmentLeaderId;

        public void OnInitialize()
        {
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(SpanningTreeCreationBroadcastPacket), (packet) => { OnSpanningTreeCreationBroadcastPacketReceived((SpanningTreeCreationBroadcastPacket)packet); }, true);

            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(FragmentJoiningRequestPacket), (packet) => { OnJoiningFragmentRequestReceived((FragmentJoiningRequestPacket)packet); });
            _packetRouter.RegisterPacketHandler(typeof(FragmentJoiningRequestValidated), (packet) => { });
        }

        [Button]
        private void StartSpanningTreeCreationWithOneCast()
        {
            _broadcaster.SendMulticast(new SpanningTreeCreationBroadcastPacket(), 1);
        }

        [Button]
        private void StartSpanningTreeCreationWithBroadcast()
        {
            _broadcaster.SendBroadcast(new SpanningTreeCreationBroadcastPacket());
        }

        private void OnSpanningTreeCreationBroadcastPacketReceived(SpanningTreeCreationBroadcastPacket packet)
        {
            _fragmentData = new GraphFragmentData(0, _networkHandling.LocalPeerInfo.peerID);
            _fragmentData.FragmentMembers.Add(_networkHandling.LocalPeerInfo);

            StartCoroutine(_waitAndSendFragmentJoiningRequestPacket());
        }

        private IEnumerator _waitAndSendFragmentJoiningRequestPacket()
        {
            yield return new WaitForSeconds(2);
            // every node create a local fragment where she is the leader and the single member

            // at the first level, finding the Minimum outgoing edge is pretty straght-forward as we only need to iterate over connections and find the highest score (which in AtomNetworking context represented the minimum outgoing edge)
            var moe = _networkHandling.GetBestConnection();
            _fragmentData.MinimumOutgoingEdgePeer = moe;

            _packetRouter.SendRequest(
                moe.peerAdress,
                new FragmentJoiningRequestPacket(_fragmentData.FragmentLevel),
                (response) =>
                {
                    // timeout
                    if (response == null)
                        return;

                    var validation = (FragmentJoiningRequestValidated)response;
                    _fragmentData.FragmentOwnerId = validation.FragmentOwnerId;
                });
        }

        private void OnJoiningFragmentRequestReceived(FragmentJoiningRequestPacket packet)
        {
            // absorbtion if the requester is lower level
            if (packet.fragmentLevel < _fragmentData.FragmentLevel)
            {
                // the members from the fragment of the requester are added to the local fragment

                // respond ?
                if (_networkHandling.Connections.TryGetValue(packet.senderID, out var joinerInfo))
                {
                    _fragmentData.FragmentMembers.Add(_networkHandling.Connections[packet.senderID]);
                }
                else
                {
                    var peerInfo = new PeerInfo(packet.senderID, packet.senderAdress);
                    _fragmentData.FragmentMembers.Add(peerInfo);
                }
                var response = (FragmentJoiningRequestValidated)packet.GetResponsePacket(packet);
                response.FragmentOwnerAdress = _networkHandling.LocalPeerInfo.peerAdress;
                response.FragmentOwnerId = _networkHandling.LocalPeerInfo.peerID;
                _packetRouter.SendResponse(packet, response);

                ContinueProcedure();
            }
            else
            {
                if (_fragmentData.MinimumOutgoingEdgePeer == null)
                {
                    _fragmentData.MinimumOutgoingEdgePeer = _networkHandling.GetBestConnection();
                }

                if (packet.senderID == _fragmentData.MinimumOutgoingEdgePeer.peerID)
                {
                    // merging if the requester is also the local best connection
                    _fragmentData.FragmentMembers.AddRange(WorldSimulationManager.nodeAddresses[packet.senderAdress].graphEntityComponent._fragmentData.FragmentMembers);
                    _fragmentData.FragmentLevel++;

                    // create a new fragment level + 1 with all members from the two merging fragments
                    // respond MERGING

                    ContinueProcedure();
                }
                else
                {
                    // just wait
                }
            }
        }

        // finding the MOE of the updated fragment
        // connect to it
        private void ContinueProcedure()
        {
            // finding the moe in a fragment is handled by a special broadcast that is issued to the fragment only
            // every fragment node will check its connections and see if there is a connection that is from another fragment 

            // simplified version for now, without messaging but direct calculus
            var outerConnections = new List<PeerInfo>();

            for (int i = 0; i < _fragmentData.FragmentMembers.Count; ++i)
            {
                var crtmember = WorldSimulationManager.nodeAddresses[_fragmentData.FragmentMembers[i].peerAdress];
                for (int j = 0; j < crtmember.networkHandling.Connections.Count; ++i)
                {
                    var connpinfo = crtmember.networkHandling.Connections.ElementAt(i);
                    var conmember = WorldSimulationManager.nodeAddresses[connpinfo.Value.peerAdress];
                    if (conmember.graphEntityComponent._fragmentData.FragmentOwnerId != _fragmentData.FragmentOwnerId)
                        outerConnections.Add(connpinfo.Value);
                }
            }

            if (outerConnections.Count > 0)
            {
                var best = float.MinValue;
                PeerInfo bestInfo = null;
                for (int i = 0; i < outerConnections.Count; ++i)
                {
                    var score = outerConnections[i].score;
                    if (score > best)
                    {
                        best = score;
                        bestInfo = outerConnections[i];
                    }
                }

                // bestInfo is moe
                _fragmentData.MinimumOutgoingEdgePeer = bestInfo;

                // sending join request and the algorithm will go on
                _packetRouter.SendRequest(
                    _fragmentData.MinimumOutgoingEdgePeer.peerAdress,
                    new FragmentJoiningRequestPacket(_fragmentData.FragmentLevel),
                    (response) =>
                    {
                        // timeout
                        if (response == null)
                            return;

                        var validation = (FragmentJoiningRequestValidated)response;
                        _fragmentData.FragmentOwnerId = validation.FragmentOwnerId;
                    });

            }
        }

        public void DisplayDebugConnectionLines()
        {
            if (_fragmentData == null)
                return;

            var pos = controller.transform.position;
            for (int i = 1; i < _fragmentData.FragmentMembers.Count; ++i)
            {
                Debug.DrawLine(pos, WorldSimulationManager.nodeAddresses[_fragmentData.FragmentMembers[i].peerAdress].transform.position);
            }
        }
    }
}
