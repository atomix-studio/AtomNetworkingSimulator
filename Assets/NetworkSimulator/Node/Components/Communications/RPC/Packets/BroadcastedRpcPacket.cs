using Assets.NetworkSimulator.Node.Components.RPC.Packets;
using Atom.CommunicationSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Atom.Components.RpcSystem
{
    /// <summary>
    /// A RPC that is broadcasted
    /// </summary>
    public class BroadcastedRpcPacket : AbstractBroadcastablePacket, IRpcPacket
    {
        public ushort RpcCode { get; set; }
        public byte[] ArgumentsPayload { get; set; } = null;

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (BroadcastedRpcPacket)(received as BroadcastedRpcPacket).MemberwiseClone();
        }
    }
}
