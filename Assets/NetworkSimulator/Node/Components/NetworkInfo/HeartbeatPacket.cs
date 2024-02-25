using Atom.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public class HeartbeatPacket : AbstractNetworkPacket, IRespondable
    {
        public string senderAdress { get; set; }
        public INetworkPacket packet => this;

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new HeartbeatResponsePacket();
        }
    }

    public class HeartbeatResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get; set; }     
        public INetworkPacket packet => this;
        [SerializerIgnore] public int requestPing { get; set; }
    }
}