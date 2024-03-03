using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Atom.Serialization;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atom.Components.RpcSystem
{
    public class RpcComponent : MonoBehaviour, INodeComponent
    {
        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private PacketRouter _packetRouter;

        // in a real node environment, rpcs would be a static collection, so as the registering method
        private Dictionary<ushort, Delegate> _rpcHandlers = new Dictionary<ushort, Delegate>();
        private Dictionary<string, ushort> _rpcIdentifiers = new Dictionary<string, ushort>();

        private ushort _rpcIdGenerator = 0;

        public NodeEntity controller { get; set ; }

        public void OnInitialize()
        {
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(BroadcastedRpcPacket), OnReceivedBroadcastableRpcPacket);
            _packetRouter.RegisterPacketHandler(typeof(RpcPacket), OnReceivedRpcPacket);
        }

        public void RegisterRpc(string rpcMethodName, Delegate rpcReceivedDelegate)
        {
            _rpcHandlers.Add(_rpcIdGenerator, rpcReceivedDelegate);
            _rpcIdentifiers.Add(rpcMethodName, _rpcIdGenerator);
            _rpcIdGenerator++;
        }        

        public void SendRpc(PeerInfo target, string rpcMethodName, params object[] args)
        {
            // get the packet from the rpc
            var packet = new RpcPacket();
            packet.RpcCode = _rpcIdentifiers[rpcMethodName];
            packet.ArgumentsPayload = AtomSerializer.Serialize(args);

            // send it via packet router
            _packetRouter.Send(target.peerAdress, packet);
        }

        public void SendRpcBroadcasted(string rpcMethodName, params object[] args)
        {
            // get the packet from the npc
            // send it via broadcaster
        }

        public void SendRpcGossip(string rpcMethodName, params object[] args)
        {
            // get the packet from the npc
            // send it via packet router
        }

        private void OnReceivedBroadcastableRpcPacket(INetworkPacket packet)
        {
            var broadcastableRpc = (BroadcastedRpcPacket)packet;
            _onReceivedRpcInternal(broadcastableRpc.RpcCode, broadcastableRpc.ArgumentsPayload);
            _broadcaster.RelayBroadcast((IBroadcastablePacket)packet);
        }

        private void OnReceivedRpcPacket(INetworkPacket packet)
        {
            var rpc = (RpcPacket)packet;
            _onReceivedRpcInternal(rpc.RpcCode, rpc.ArgumentsPayload);
        }

        private void _onReceivedRpcInternal(ushort rpcCode, byte[] payload)
        {
            // the deserialization of the payload will be the job of AtomSerializer as well as the deserialization of the packet itself after the transportLayer receive and before the transport layer send
            _rpcs[rpcCode].DynamicInvoke(payload);
        }
    }
}
