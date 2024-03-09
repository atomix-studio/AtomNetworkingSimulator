using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    [Serializable]
    public class FragmentJoiningRequestPacket : AbstractNetworkPacket, IRespondable
    {
        public FragmentJoiningRequestPacket(int fragmentLevel, long fragmentId)
        {
            this.joinerfragmentLevel = fragmentLevel;
            this.joinerFragmentId = fragmentId;
        }

        public string senderAdress { get ; set ; }

        public int joinerfragmentLevel { get ; set ; }
        public long joinerFragmentId { get ; set ; }
        public List<long> oldFragmentIds { get ; set ; }


        public INetworkPacket packet => this;

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new FragmentJoiningRequestValidated();
        }
    }


    public enum GraphOperations
    {
        Absorbed,
        Merged,
        // when a join is received but the local node has already this node as edge
        RefreshExistingEdge
    }

    // callback from a leader to tell a node it has received and accepted to become the new fragment leader
    public class FragmentJoiningRequestValidated : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get ; set ; }

        public INetworkPacket packet => this;

        public int requestPing { get ; set ; }

        public long FragmentId { get; set; }
        public int FragmentLevel { get; set; }

        public GraphOperations graphOperation { get; set; } 

    }
}
