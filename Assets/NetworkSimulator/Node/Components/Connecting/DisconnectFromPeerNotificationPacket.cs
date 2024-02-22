using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.Connecting
{
    public class DisconnectFromPeerNotificationPacket : AbstractNetworkPacket, IRespondable
    {
        public string senderAdress { get; set; }

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            // for now we use respondable just because senderAdress handling is automated for these packets
            // but we can imagine than a node answers to the disconnection request to prevent the peer to end the disconnection 

            return null;
        }
    }
}
