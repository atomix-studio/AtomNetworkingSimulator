using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    [Serializable]
    public class GraphFragmentData
    {
        public int FragmentLevel { get; set; } = 0;
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

    public class GraphEntityComponent : INodeComponent
    {
        public NodeEntity controller { get ; set ; }
        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private PacketRouter _packetRouter;
        [Inject] private NetworkHandlingComponent _networkHandling;

        public void OnInitialize()
        {

        }
    }
}
