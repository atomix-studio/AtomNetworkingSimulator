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
        public long FragmentOwnerId = -1; // PEER ID OF THE FRAGMENT LEAD
        public int FragmentLevel = 0;

        public List<PeerInfo> FragmentMembers = new List<PeerInfo>();

        public (PeerInfo, PeerInfo) MinimumOutgoingEdgePeer = (null, null);

        public GraphFragmentData(short fragmentLevel, long fragmentLeaderId)
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
        public void StartSpanningTreeCreationWithBroadcast()
        {
            _broadcaster.SendBroadcast(new SpanningTreeCreationBroadcastPacket());
            OnSpanningTreeCreationBroadcastPacketReceived(new SpanningTreeCreationBroadcastPacket());
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
            StartCoroutine(MoeSearching());
        }

        private void OnJoiningFragmentRequestReceived(FragmentJoiningRequestPacket packet)
        {
            if (_fragmentData.FragmentMembers.Exists(t => t.peerID == packet.senderID))
                return;

            if (_fragmentData.FragmentOwnerId != _networkHandling.LocalPeerInfo.peerID)
            {
                Debug.Log($"Request received by {packet.senderID} but not a leader {_networkHandling.LocalPeerInfo.peerID}");
            }
            // absorbtion if the requester is lower level
            if (packet.fragmentLevel < _fragmentData.FragmentLevel)
            {
                Debug.Log(_networkHandling.LocalPeerInfo.peerAdress + "absorbing" + packet.senderAdress);

                // the members from the fragment of the requester are added to the local fragment

                // respond ?
                if (_networkHandling.Connections.TryGetValue(packet.senderID, out var joinerInfo))
                {
                    _fragmentData.FragmentMembers.AddRange(WorldSimulationManager.nodeAddresses[packet.senderAdress].graphEntityComponent._fragmentData.FragmentMembers);
                }
                else
                {
                    //   var peerInfo = new PeerInfo(packet.senderID, packet.senderAdress);
                    _fragmentData.FragmentMembers.AddRange(WorldSimulationManager.nodeAddresses[packet.senderAdress].graphEntityComponent._fragmentData.FragmentMembers);
                }

                var response = (FragmentJoiningRequestValidated)packet.GetResponsePacket(packet);
                response.FragmentOwnerAdress = _networkHandling.LocalPeerInfo.peerAdress;
                response.FragmentOwnerId = _networkHandling.LocalPeerInfo.peerID;
                response.FragmentLevel = _fragmentData.FragmentLevel;
                _packetRouter.SendResponse(packet, response);

                ContinueProcedure();
            }
            else
            {               
                if (packet.senderID == _fragmentData.MinimumOutgoingEdgePeer.Item2.peerID)
                {
                    Debug.LogError(_networkHandling.LocalPeerInfo.peerAdress + "merging" + packet.senderAdress);

                    // merging if the requester is also the local best connection
                    var mergedEntityFragmentLeader = WorldSimulationManager.nodeAddresses[packet.senderAdress];
                    mergedEntityFragmentLeader.graphEntityComponent._fragmentData.FragmentLevel++;
                    mergedEntityFragmentLeader.graphEntityComponent._fragmentData.FragmentOwnerId = _fragmentData.FragmentOwnerId;
                    var mergedMembers = new List<PeerInfo>(mergedEntityFragmentLeader.graphEntityComponent._fragmentData.FragmentMembers);

                    // mergedEntityFragmentLeader.graphEntityComponent._fragmentData.FragmentMembers.Add(new PeerInfo(_networkHandling.LocalPeerInfo));
                    mergedEntityFragmentLeader.graphEntityComponent._fragmentData.FragmentMembers.AddRange(_fragmentData.FragmentMembers);

                    //_fragmentData.FragmentMembers.Add(new PeerInfo( mergedEntityFragmentLeader.networkHandling.LocalPeerInfo));
                    _fragmentData.FragmentMembers.AddRange(mergedMembers);
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

        [Button]
        // finding the MOE of the updated fragment
        // connect to it
        private void ContinueProcedure()
        {
            // finding the moe in a fragment is handled by a special broadcast that is issued to the fragment only
            // every fragment node will check its connections and see if there is a connection that is from another fragment 

            // simplified version for now, without messaging but direct calculus
            var outerConnections = new List<(PeerInfo, PeerInfo)>();

            /*for (int i = 0; i < _fragmentData.FragmentMembers.Count; ++i)
            {
                var crtmember = WorldSimulationManager.nodeAddresses[_fragmentData.FragmentMembers[i].peerAdress];
                for (int j = 0; j < crtmember.networkHandling.Connections.Count; ++j)
                {
                    var connpinfo = crtmember.networkHandling.Connections.ElementAt(j);
                    var conmember = WorldSimulationManager.nodeAddresses[connpinfo.Value.peerAdress];
                    if (conmember.graphEntityComponent._fragmentData.FragmentOwnerId != _fragmentData.FragmentOwnerId)
                        outerConnections.Add(connpinfo.Value);
                }
            }*/

            for (int i = 0; i < WorldSimulationManager.nodeAddresses.Count; ++i)
            {
                var crtmember = WorldSimulationManager.nodeAddresses.ElementAt(i);
                if(crtmember.Value.graphEntityComponent._fragmentData.FragmentOwnerId == _fragmentData.FragmentOwnerId)
                {
                    // local moe
                    var crtOuterConnections = new List<(PeerInfo, PeerInfo)>();

                    for (int j = 0; j < crtmember.Value.networkHandling.Connections.Count; ++j)
                    {
                        var connpinfo = crtmember.Value.networkHandling.Connections.ElementAt(j);
                        var conmember = WorldSimulationManager.nodeAddresses[connpinfo.Value.peerAdress];
                        if (conmember.graphEntityComponent._fragmentData.FragmentOwnerId != _fragmentData.FragmentOwnerId)
                        {
                            crtOuterConnections.Add(new (crtmember.Value.networkHandling.LocalPeerInfo, connpinfo.Value));
                        }
                    }

                    if(crtOuterConnections.Count > 0)
                    {
                        crtOuterConnections.Sort((a, b) => a.Item2.score.CompareTo(b.Item2.score));
                        outerConnections.Add(crtOuterConnections[0]);
                    }
                }
            }

            if (outerConnections.Count > 0)
            {
                var best = float.MinValue;
                PeerInfo bestOuterMoe = null;
                PeerInfo bestInnerMoe = null;
                for (int i = 0; i < outerConnections.Count; ++i)
                {
                    var score = outerConnections[i].Item2.score;
                    if (score > best)
                    {
                        best = score;
                        // in-fragment node for the min outgoing edge
                        bestInnerMoe = outerConnections[i].Item1;
                        // outèfragment node for the min outgoing edge
                        bestOuterMoe = outerConnections[i].Item2;
                    }
                }

                // bestInfo is moe
                _fragmentData.MinimumOutgoingEdgePeer = new (bestInnerMoe, bestOuterMoe);

                // sending join request and the algorithm will go on
                var node = WorldSimulationManager.nodeAddresses[bestInnerMoe.peerAdress];
                node.graphEntityComponent.SendFragmentJoiningRequest(bestOuterMoe);

                
            }
            else
            {
                Debug.Log("No outer connection found from fragment " + _fragmentData.FragmentOwnerId);
            }
        }

        public void SendFragmentJoiningRequest(PeerInfo outgoingEdgeNodeInfo)
        {
            _packetRouter.SendRequest(
                    outgoingEdgeNodeInfo.peerAdress,
                    new FragmentJoiningRequestPacket(_fragmentData.FragmentLevel),
                    (response) =>
                    {
                        // timeout
                        Debug.Log("fragment joining request timed out.");
                        if (response == null)
                            return;

                        var validation = (FragmentJoiningRequestValidated)response;
                        _fragmentData.FragmentOwnerId = validation.FragmentOwnerId;
                    });

        }

        private IEnumerator MoeSearching()
        {
            float interval = UnityEngine.Random.Range(.75f, 1.2f);
            var wfs = new WaitForSeconds(interval);

            while (_fragmentData.FragmentOwnerId == _networkHandling.LocalPeerInfo.peerID)
            {
                yield return wfs;
                ContinueProcedure();
            }
        }

        public void StopSearching()
        {
            StopAllCoroutines();
        }

        public void DisplayDebugConnectionLines()
        {
            if (_fragmentData == null)
                return;

            var pos = controller.transform.position;

            if (_fragmentData.FragmentOwnerId == _networkHandling.LocalPeerInfo.peerID)
                for (int i = 1; i < _fragmentData.FragmentMembers.Count; ++i)
                {
                    Debug.DrawLine(pos, WorldSimulationManager.nodeAddresses[_fragmentData.FragmentMembers[i].peerAdress].transform.position, Color.magenta);
                }
            else
                for (int i = 1; i < _fragmentData.FragmentMembers.Count; ++i)
                {
                    Debug.DrawLine(pos, WorldSimulationManager.nodeAddresses[_fragmentData.FragmentMembers[i].peerAdress].transform.position, Color.green);
                }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (_fragmentData == null)
                return;

            if (_fragmentData.FragmentOwnerId == _networkHandling.LocalPeerInfo.peerID)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(controller.transform.position + Vector3.up * 2, Vector3.one);
                Gizmos.color = Color.white;
            }
        }

#endif
    }
}
