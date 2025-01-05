using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    public class TakeLeadRequestPacket : AbstractBroadcastablePacket
    {
        public TakeLeadRequestPacket() { }  

        public TakeLeadRequestPacket(long propectId, string propectAdress)
        {
            this.prospectId = propectId;
            this.prospectAdress = propectAdress;
        }

        public long prospectId { get; set ; }
        public string prospectAdress { get; set ; }

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (INetworkPacket)((TakeLeadRequestPacket)received).MemberwiseClone();
        }

    }
}
