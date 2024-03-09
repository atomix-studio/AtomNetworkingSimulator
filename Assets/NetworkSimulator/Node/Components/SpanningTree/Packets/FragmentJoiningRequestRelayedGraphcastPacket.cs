using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    public class FragmentJoiningRequestRelayedGraphcastPacket : AbstractBroadcastablePacket
    {
        public FragmentJoiningRequestRelayedGraphcastPacket(long senderId, string senderAdress, int joinerfragmentLevel, long joinerFragmentId)
        {
            this.originId = senderId;
            this.originAdress = senderAdress;
            this.joinerfragmentLevel = joinerfragmentLevel;
            this.joinerFragmentId = joinerFragmentId;
        }

        public long originId { get; set; }
        public string originAdress { get; set; }
        public int joinerfragmentLevel { get; set; }
        public long joinerFragmentId { get; set; }

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (INetworkPacket)(received as FragmentJoiningRequestRelayedGraphcastPacket).MemberwiseClone();
        }
    }
}
