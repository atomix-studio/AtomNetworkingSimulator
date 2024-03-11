using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    public class FragmentUpdatingBroadcastPacket : AbstractBroadcastablePacket
    {
        public long newFragmentId { get; set; }
        public int newFragmentLevel { get; set; }

        public FragmentUpdatingBroadcastPacket(long newFragmentId, int newFragmentLevel)
        {
            this.newFragmentId = newFragmentId;
            this.newFragmentLevel = newFragmentLevel;
        }

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (INetworkPacket)((FragmentUpdatingBroadcastPacket)received).MemberwiseClone();
        }
    }
}
