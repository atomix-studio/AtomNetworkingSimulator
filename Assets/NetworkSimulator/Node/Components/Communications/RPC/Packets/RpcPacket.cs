using Assets.NetworkSimulator.Node.Components.RPC.Packets;
using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.RpcSystem
{
    /// <summary>
    /// A RPC that is sent to given targets (casted,multicasted)
    /// </summary>
    public class RpcPacket : AbstractNetworkPacket, IRpcPacket
    {
        public ushort RpcCode { get; set; }
        public byte[] ArgumentsPayload { get; set; } = null;
    }
}
