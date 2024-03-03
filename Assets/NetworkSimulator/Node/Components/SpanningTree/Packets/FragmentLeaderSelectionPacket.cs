using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    public class FragmentLeaderSelectionPacket : AbstractNetworkPacket, IRespondable
    {
        public FragmentLeaderSelectionPacket(short fragmentLevel)
        {
            this.fragmentLevel = fragmentLevel;
        }

        public string senderAdress { get ; set ; }
        public short fragmentLevel { get ; set ; }

        public INetworkPacket packet => this;

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new FragmentLeaderValidationPacket();
        }
    }

    // callback from a leader to tell a node it has received and accepted to become the new fragment leader
    public class FragmentLeaderValidationPacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get ; set ; }

        public INetworkPacket packet => this;

        public int requestPing { get ; set ; }
    }
}
