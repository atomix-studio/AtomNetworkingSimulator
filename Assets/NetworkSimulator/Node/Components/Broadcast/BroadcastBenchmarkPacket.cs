using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    internal class BroadcastBenchmarkPacket : AbstractBroadcastablePacket
    {
        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return new BroadcastBenchmarkPacket((BroadcastBenchmarkPacket)received);
        }

        public BroadcastBenchmarkPacket() { }
        public BroadcastBenchmarkPacket(BroadcastBenchmarkPacket packet)
        {
            this.packetTypeIdentifier = packet.packetTypeIdentifier;
            this.packetUniqueId = packet.packetUniqueId;
            this.senderID = packet.senderID;
            this.sentTime = packet.sentTime;
            this.broadcasterID = packet.broadcasterID;
            this.broadcastID = packet.broadcastID;
        }
    }
}
