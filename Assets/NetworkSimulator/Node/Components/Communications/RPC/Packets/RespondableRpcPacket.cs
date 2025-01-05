using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.RpcSystem
{
    public class RespondableRpcPacket : RpcPacket, IRespondable
    {
        public string senderAdress { get ; set; }

        public INetworkPacket packet => this;

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new ResponseRpcPacket();
        }
    }

    public class ResponseRpcPacket : RpcPacket, IResponse
    {
        public long callerPacketUniqueId { get ; set; }

        public INetworkPacket packet => this;

        public int requestPing { get; set; }
    }
}
