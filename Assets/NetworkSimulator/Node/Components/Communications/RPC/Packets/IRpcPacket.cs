using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.NetworkSimulator.Node.Components.RPC.Packets
{
    public interface IRpcPacket
    {
        public ushort RpcCode { get; set; }
        public byte[] ArgumentsPayload { get; set; }

    }
}
