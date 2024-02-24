using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.Connecting
{
    public class ConnectionRequestPacket : AbstractNetworkPacket, IRespondable
    {
        public ConnectionRequestPacket()
        {
        }

        public ConnectionRequestPacket(byte networkInfoCallersCount, byte networkInfoListennersCount)
        {
            this.networkInfoCallersCount = networkInfoCallersCount;
            this.networkInfoListennersCount = networkInfoListennersCount;
        }

        public string senderAdress { get ; set ; }

        /// <summary>
        /// number of callers the node have
        /// </summary>
        public byte networkInfoCallersCount { get; set; }

        /// <summary>
        /// number of listenners the node have
        /// </summary>
        public byte networkInfoListennersCount { get; set; }

        public INetworkPacket packet => this;

        public IResponse GetResponsePacket(IRespondable answerPacket)
        {
            return new ConnectionRequestResponsePacket();
        }
    }

    public class ConnectionRequestResponsePacket : AbstractNetworkPacket, IResponse
    {
        public long callerPacketUniqueId { get ; set; }

        /// <summary>
        /// When a connection request is accepted by a node,the node add the requester in its callers (aka the nodes that speaks to him)
        /// On receiving this response a node adds the sender in its listeners (aka the nodes he speaks to)
        /// </summary>
        public bool isAccepted { get; set; }

        public INetworkPacket packet => this;
    }
}
