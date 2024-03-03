using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Sirenix.OdinInspector;
using System;
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
        public GraphFragmentData(short fragmentLevel, string fragmentLeaderId)
        {
            FragmentLevel = fragmentLevel;
            FragmentLeaderId = fragmentLeaderId;
        }

        public short FragmentLevel { get; set; } = 0;
        public string FragmentLeaderId { get; set; } = string.Empty; // PEER ID OF THE FRAGMENT LEAD
    }

    [Serializable]
    public class GraphFragmentLeaderData
    {
        /// <summary>
        /// IDS of the nodes that are within the fragment (if the local node is leader)
        /// those nodes have been notifyed for the add with the FragmentLeaderSelectionPacket and response request
        /// </summary>
        public List<string> LeavesID { get; set; }
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
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(SpanningTreeCreationBroadcastPacket), (packet) => { OnSpanningTreeCreationBroadcastPacketReceived((SpanningTreeCreationBroadcastPacket)packet); });

            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(FragmentLeaderSelectionPacket), (packet) => { });
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

            // every node create a local fragment where she is the leader and the single member

            // at the first level, finding the Minimum outgoing edge is pretty straght-forward as we only need to iterate over connections and find the highest score (which in AtomNetworking context represented the minimum outgoing edge)
            var moe = _networkHandling.GetBestConnection();
            _packetRouter.Send(moe.peerAdress, new FragmentLeaderSelectionPacket(_fragmentData.FragmentLevel));
        }

        private void OnFragmentLeaderSelectionPacketReceived(FragmentLeaderSelectionPacket packet)
        {

        }
    }
}
