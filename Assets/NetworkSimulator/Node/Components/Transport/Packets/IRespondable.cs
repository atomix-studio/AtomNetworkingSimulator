using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public interface IRespondable
    {
        public string senderAdress { get; set; }
        public INetworkPacket packet { get; }

        public IResponse GetResponsePacket(IRespondable answerPacket);
    }
}
