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
        public long FragmentID = -1; // PEER ID OF THE FRAGMENT LEAD
        public int FragmentLevel = 0;

        public List<PeerInfo> FragmentMembers = new List<PeerInfo>();

        public MinimumOutgoingEdge MinimumOutgoingEdge = null;

        public GraphFragmentData(short fragmentLevel, long fragmentLeaderId)
        {
            FragmentLevel = fragmentLevel;
            FragmentID = fragmentLeaderId;
        }

    }

    /// <summary>
    /// Representation of a connection with an outer node in the graph
    /// When a connection is accepted by a node, both sides creates an instance of edge with the adress of the other node
    /// All graph edges concatened at the network level represents the MST at the end of the procedure
    /// </summary>
    [Serializable, HideLabel]
    public class GraphEdge
    {
        [HorizontalGroup("GraphEdge")] public long EdgeId;
        [HorizontalGroup("GraphEdge")] public string EdgeAdress;

        public GraphEdge(long edgeId, string edgeAdress)
        {
            EdgeId = edgeId;
            EdgeAdress = edgeAdress;
        }
    }

    [Serializable]
    public class MinimumOutgoingEdge
    {
        public PeerInfo InnerFragmentNode;
        public PeerInfo OuterFragmentNode;
        public long OuterFragmentId;

        public MinimumOutgoingEdge(PeerInfo innerFragmentNode, PeerInfo outerFragmentNode, long outerFragmentId)
        {
            InnerFragmentNode = innerFragmentNode;
            OuterFragmentNode = outerFragmentNode;
            OuterFragmentId = outerFragmentId;
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
        public bool IsPendingOutgoingEdge = false;

        public long LocalFragmentId => _fragmentData.FragmentID;
        public bool IsMinimumOutgoinEdge
        {
            get
            {
                return _fragmentData != null && _fragmentData.MinimumOutgoingEdge != null && _fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerID == _networkHandling.LocalPeerInfo.peerID;
            }
        }

        [SerializeField] private List<GraphEdge> _graphEdges = new List<GraphEdge>();

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
            _graphEdges.Clear();

            _fragmentData = new GraphFragmentData(0, _networkHandling.LocalPeerInfo.peerID);
            _fragmentData.FragmentMembers.Add(_networkHandling.LocalPeerInfo);
            IsPendingOutgoingEdge = true;

            StartCoroutine(_waitAndSendFragmentJoiningRequestPacket());
        }

        private IEnumerator _waitAndSendFragmentJoiningRequestPacket()
        {
            yield return new WaitForSeconds(2);
            // every node create a local fragment where she is the leader and the single member

            // at the first level, finding the Minimum outgoing edge is pretty straght-forward as we only need to iterate over connections and find the highest score (which in AtomNetworking context represented the minimum outgoing edge)
            StartCoroutine(MoeSearching());

            //ContinueProcedure();

        }

        private void OnJoiningFragmentRequestReceived(FragmentJoiningRequestPacket joiningRequestPacket)
        {
            if (joiningRequestPacket.joinerFragmentId == LocalFragmentId)
                return;

            if (_fragmentData.FragmentID != _networkHandling.LocalPeerInfo.peerID && !IsPendingOutgoingEdge && !IsMinimumOutgoinEdge)
            {
                Debug.Log($"Request received by {joiningRequestPacket.senderID} but not a leader {_networkHandling.LocalPeerInfo.peerID}");

                // if it should be absorbed or merged, the join request have to be handled
                if (joiningRequestPacket.joinerfragmentLevel < _fragmentData.FragmentLevel
                    || joiningRequestPacket.joinerFragmentId == _fragmentData.MinimumOutgoingEdge.OuterFragmentId)
                {
                    if(_fragmentData.MinimumOutgoingEdge != null)
                    {
                        var innerEdgeNode = WorldSimulationManager.nodeAddresses[_fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerAdress];
                        innerEdgeNode.graphEntityComponent.OnJoiningFragmentRequestReceived(joiningRequestPacket);
                    }
                    else
                    {
                        // relay to the fragment leader that will launch the find moe and directly relay the request to it
                        Debug.LogError("No MOE yet, relaying to leader required.");
                    }
                }
                // if the requester is the moe (outer), he will 
                return;
            }

            // absorbtion if the requester is lower level
            if (joiningRequestPacket.joinerfragmentLevel < _fragmentData.FragmentLevel)
            {
                AbsorbFragment(joiningRequestPacket);
            }
            else
            {
                //                if (joiningRequestPacket.senderId == _fragmentData.MinimumOutgoingEdge.OuterFragmentNode.peerId)

                if(_fragmentData.MinimumOutgoingEdge == null)
                {
                    return;
                }

                if (joiningRequestPacket.joinerFragmentId == _fragmentData.MinimumOutgoingEdge.OuterFragmentId)
                {
                    Debug.LogError(_networkHandling.LocalPeerInfo.peerAdress + " merging" + joiningRequestPacket.senderAdress);

                    // create a new fragment level + 1 with all members from the two merging fragments
                    // respond MERGING
                    _fragmentData.FragmentLevel++;

                    foreach (var node in WorldSimulationManager.nodeAddresses)
                    {
                        if (node.Value.graphEntityComponent.LocalFragmentId == LocalFragmentId)
                        {
                            // incrementing the fragment level for all the current fragment nodes
                            node.Value.graphEntityComponent._fragmentData.FragmentLevel = _fragmentData.FragmentLevel;
                        }
                    }

                    _createGraphEdge(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress);
                    IsPendingOutgoingEdge = false;

                    var response = (FragmentJoiningRequestValidated)joiningRequestPacket.GetResponsePacket(joiningRequestPacket);
                    response.FragmentId = _fragmentData.FragmentID;
                    response.FragmentLevel = _fragmentData.FragmentLevel;
                    _packetRouter.SendResponse(joiningRequestPacket, response);

                    ContinueProcedure();
                }
                else if(joiningRequestPacket.joinerFragmentId == _fragmentData.MinimumOutgoingEdge.OuterFragmentId)
                {
                    // the both sides consent to connect between each other but they are not quite decided about the minimum outgoing edge because the scores calculated by each sides differ.
                    // at this point we want a consensus between both sides.
                    Debug.LogError("Consensus needed");
                }
                else
                {
                    // just wait
                }
            }
        }

        private void AbsorbFragment(FragmentJoiningRequestPacket joiningRequestPacket)
        {
            Debug.Log(_networkHandling.LocalPeerInfo.peerAdress + "absorbing" + joiningRequestPacket.senderAdress);

            _createGraphEdge(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress);
            IsPendingOutgoingEdge = false;

            var response = (FragmentJoiningRequestValidated)joiningRequestPacket.GetResponsePacket(joiningRequestPacket);
            response.FragmentId = _fragmentData.FragmentID;
            response.FragmentLevel = _fragmentData.FragmentLevel;
            _packetRouter.SendResponse(joiningRequestPacket, response);

            ContinueProcedure();
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
            var outerFragmentId = new List<long>();

            foreach(var n in WorldSimulationManager.nodeAddresses)
            {
                if (n.Value.graphEntityComponent.LocalFragmentId != LocalFragmentId)
                    continue;

                for (int j = 0; j < n.Value.networkHandling.Connections.Count; ++j)
                {
                    var connpinfo = n.Value.networkHandling.Connections.ElementAt(j);
                    var conmember = WorldSimulationManager.nodeAddresses[connpinfo.Value.peerAdress];
                    if (conmember.graphEntityComponent._fragmentData.FragmentID != _fragmentData.FragmentID)
                    {
                        outerConnections.Add(new(n.Value.networkHandling.LocalPeerInfo, connpinfo.Value));
                        outerFragmentId.Add(conmember.graphEntityComponent.LocalFragmentId);
                    }
                }
            }

            if (outerConnections.Count > 0)
            {
                var best = float.MinValue;
                var bestIndex = 0;

                for (int i = 0; i < outerConnections.Count; ++i)
                {
                    var score = outerConnections[i].Item2.score;
                    if (score > best)
                    {
                        best = score;
                        bestIndex = i;
                    }
                }

                // bestInfo is moe
                _fragmentData.MinimumOutgoingEdge = new MinimumOutgoingEdge(outerConnections[bestIndex].Item1, outerConnections[bestIndex].Item2, outerFragmentId[bestIndex]);
                foreach (var n in WorldSimulationManager.nodeAddresses)
                {
                    if (n.Value.graphEntityComponent.LocalFragmentId == LocalFragmentId)
                    {
                        if (n.Value.graphEntityComponent._fragmentData != null)
                            n.Value.graphEntityComponent._fragmentData.MinimumOutgoingEdge = _fragmentData.MinimumOutgoingEdge;
                    }
                }

                // sending join request and the algorithm will go on
                var node = WorldSimulationManager.nodeAddresses[_fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerAdress];
                node.graphEntityComponent.IsPendingOutgoingEdge = true;
                node.graphEntityComponent.SendFragmentJoiningRequest(_fragmentData.MinimumOutgoingEdge.OuterFragmentNode);
            }
            else
            {
                _fragmentData.MinimumOutgoingEdge = null;
                Debug.Log("No outer connection found from fragment " + _fragmentData.FragmentID);
            }
        }

        // the join is sent by the inner node of the minimum outgoing edge which connects the two fragments
        // if accepted, the sending node will become a graph edge
        public void SendFragmentJoiningRequest(PeerInfo outgoingEdgeNodeInfo)
        {
            _packetRouter.SendRequest(
                    outgoingEdgeNodeInfo.peerAdress,
                    new FragmentJoiningRequestPacket(_fragmentData.FragmentLevel, _fragmentData.FragmentID),
                    (response) =>
                    {
                        // timeout
                        Debug.Log("fragment joining request timed out.");
                        if (response == null)
                            return;

                        var validation = (FragmentJoiningRequestValidated)response;

                        _createGraphEdge(outgoingEdgeNodeInfo.peerID, outgoingEdgeNodeInfo.peerAdress);
                        IsPendingOutgoingEdge = false;

                        var oldfragmentId = _fragmentData.FragmentID;
                        // when the join request is accepted, the requested node has decided wether its a merge or an absorb and has
                        // updated the fragment level if needed.
                        // we simulate here without message the updating of all the fragment ids of the nodes of the fragment that has request a join
                        foreach (var node in WorldSimulationManager.nodeAddresses)
                        {
                            if (node.Value.graphEntityComponent.LocalFragmentId == oldfragmentId)
                            {
                                node.Value.graphEntityComponent._fragmentData.FragmentLevel = validation.FragmentLevel;
                                node.Value.graphEntityComponent._fragmentData.FragmentID = validation.FragmentId;
                            }
                        }
                    });

        }

        private void _createGraphEdge(long otherNodeId, string otherNodeAdress)
        {
            _graphEdges.Add(new GraphEdge(otherNodeId, otherNodeAdress));
        }

        private IEnumerator MoeSearching()
        {
            float interval = UnityEngine.Random.Range(.75f, 1.2f);
            var wfs = new WaitForSeconds(interval);

            while (_fragmentData.FragmentID == _networkHandling.LocalPeerInfo.peerID)
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

            for (int i = 0; i < _graphEdges.Count; ++i)
            {
                var entity = WorldSimulationManager.nodeAddresses[_graphEdges[i].EdgeAdress];
                Debug.DrawLine(entity.transform.position, transform.position, Color.red);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (_fragmentData == null)
                return;

            if (_fragmentData.FragmentID == _networkHandling.LocalPeerInfo.peerID)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(controller.transform.position + Vector3.up * 2, Vector3.one);
                Gizmos.color = Color.white;
            }
        }

#endif
    }
}
