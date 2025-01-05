using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    public class LeaderHeartbeatPacket : AbstractBroadcastablePacket
    {
        public LeaderHeartbeatPacket(long fragmentLevel)
        {
            FragmentLevel = fragmentLevel;
        }

        public long FragmentId => broadcasterID;
        public long FragmentLevel { get; set; } 


        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (INetworkPacket)((LeaderHeartbeatPacket)received).MemberwiseClone();
        }
    }
}
