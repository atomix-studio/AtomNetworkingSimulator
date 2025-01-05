using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    /// <summary>
    /// Packet sent by a leader to the inner minimum outgoing edge that will become the new leader of the fragment and handle the JOIN request over other fragments
    /// </summary>
    public class FragmentLeaderSelectingPacket : AbstractBroadcastablePacket
    {
        public FragmentLeaderSelectingPacket(long oldLeaderId, long newLeaderId, long outgoingEdgeId)
        {
            OldLeaderId = oldLeaderId;
            NewLeaderId = newLeaderId;
            OutgoingEdgeId = outgoingEdgeId;
        }

        public long OldLeaderId { get; set; }
        public long NewLeaderId { get; set; }

        // the newleader id should have cached its minimum outgoing edge when the old leader requested it so this is potentially useless data
        public long OutgoingEdgeId { get; set; }

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (INetworkPacket)(received as FragmentLeaderSelectingPacket).MemberwiseClone();
        }
    }
}
