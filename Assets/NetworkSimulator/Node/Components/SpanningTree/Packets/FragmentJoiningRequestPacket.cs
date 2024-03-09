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

        public INetworkPacket packet => this;

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return null;
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
    public class FragmentJoiningRequestValidated : AbstractNetworkPacket
    {
        public FragmentJoiningRequestValidated(long senderId, string senderAdress)
        {
            this.senderId = senderId;
            this.senderAdress = senderAdress;
        }

        public FragmentJoiningRequestValidated(long senderId, string senderAdress, long fragmentId, int fragmentLevel, GraphOperations graphOperation)
        {
            this.senderId = senderId;
            this.senderAdress = senderAdress;
            FragmentId = fragmentId;
            FragmentLevel = fragmentLevel;
            this.graphOperation = graphOperation;
        }

        public long senderId { get ; set ; }
        public string senderAdress { get; set; }
        public long FragmentId { get; set; }
        public int FragmentLevel { get; set; }

        public GraphOperations graphOperation { get; set; } 

    }
}
