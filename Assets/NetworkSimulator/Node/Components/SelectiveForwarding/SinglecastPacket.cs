using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Broadcasting.SelectiveForwarding
{
    /// <summary>
    /// Experiencing with a concept of a packet relayed only 1 by 1 (with confirmation of reception awaited by the sender that will try next connection if failed)
    /// The packet will keep going one by one over the network until it reaches a validation 
    /// </summary>
    public class SinglecastPacket : AbstractNetworkPacket, IRespondable
    {
        public SinglecastPacket(string casterAdress, long casterID, long targetID, int cycles)
        {            
            this.casterAdress = casterAdress;
            this.casterID = casterID;
            this.targetID = targetID;
            this.cycles = cycles++;
        }

        public string casterAdress { get; set; }
        public long casterID { get; set; }

        public string senderAdress { get ; set ; }

        public INetworkPacket packet => this;

        // we simulate the case of we want to find a node by its id
        public long targetID { get; set; }
        public int cycles { get; set; }


        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new SinglecastReceivedConfirmationPacket();
        }
    }

    public class SinglecastReceivedConfirmationPacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set; }

        public INetworkPacket packet => this;

        public int requestPing { get; set; }
    }
}
