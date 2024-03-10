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
        public TakeLeadRequestPacket(long propectId, string propectAdress)
        {
            this.propectId = propectId;
            this.propectAdress = propectAdress;
        }

        public long propectId { get; set ; }
        public string propectAdress { get; set ; }

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (INetworkPacket)((TakeLeadRequestPacket)received).MemberwiseClone();
        }

    }
}
