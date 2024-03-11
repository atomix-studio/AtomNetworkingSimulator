using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.Components.GraphNetwork;
using Atom.DependencyProvider;
using Atom.Serialization;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.RpcSystem
{
    /// <summary>
    /// INTRODUCTION :
    /// "Remote Procedure Call is a software communication protocol that one program can use to request a service 
    /// from a program located in another computer on a network without having to understand the network's details."
    /// 
    /// 
    /// So the Remote Procedure Call Component allows any system implementation from a Node to register and use remote calls in the network 
    /// without the extra workload of actually designing and registering a specific packet.
    /// 
    /// RPC's are by essence at a higher level from packets, they will be less efficient as they require extra dynamic serialization work.
    /// All low-level/optimized network calls should be directly done by using the packet system (used by all the core components of the Node)
    /// </summary>
    public class RpcComponent : MonoBehaviour, INodeComponent
    {
        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private GraphcasterComponent _graphcaster;
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

            // rpc benchmarking (and sample)
            // the action<string> needs to be casted to the method 
            RegisterRpc((Action<string>)Rpc_benchmark);
        }

        /// <summary>
        /// Registers a simple Remote procedure call 
        /// </summary>
        /// <param name="rpcMethodName"></param>
        /// <param name="rpcReceivedDelegate"></param>
        public void RegisterRpc(Delegate rpcReceivedDelegate)
        {
            _rpcHandlers.Add(_rpcIdGenerator, rpcReceivedDelegate);
            _rpcIdentifiers.Add(rpcReceivedDelegate.Method.Name, _rpcIdGenerator);
            _rpcIdGenerator++;
        }

        /// <summary>
        /// Registers a remote procedure call that automacitally handle a response as from its rpc method callback (response from the target)
        /// </summary>
        /// <param name="rpcReceivedDelegate"></param>
        public void RegisterRPCRequest(Delegate rpcReceivedDelegate, Delegate rpcResponseReceivedDelegate)
        {
            RegisterRpc(rpcReceivedDelegate);
            RegisterRpc(rpcResponseReceivedDelegate);
        }
/*
        public async Task<object[]> SendRpcRequest(PeerInfo target, string rpcMethodName, params object[] args)
        {

        }
*/
        public void SendRpc(PeerInfo target, string rpcMethodName, params object[] args)
        {
            // get the packet from the rpc
            var packet = new RpcPacket();
            packet.RpcCode = _rpcIdentifiers[rpcMethodName];
            packet.ArgumentsPayload = AtomSerializer.SerializeDynamic(packet.RpcCode, args);

            // send it via packet router
            _packetRouter.Send(target.peerAdress, packet);
        }

        public void SendRpcBroadcasted(string rpcMethodName, params object[] args)
        {
            // get the packet from the rpc
            var packet = new BroadcastedRpcPacket();
            packet.RpcCode = _rpcIdentifiers[rpcMethodName];
            packet.ArgumentsPayload = AtomSerializer.SerializeDynamic(packet.RpcCode, args);

            // send it via broadcaster
            _broadcaster.SendBroadcast(packet);
        }

        public void SendRpcGraphcasted(string rpcMethodName, params object[] args)
        {
            // get the packet from the rpc
            var packet = new BroadcastedRpcPacket();
            packet.RpcCode = _rpcIdentifiers[rpcMethodName];
            packet.ArgumentsPayload = AtomSerializer.SerializeDynamic(packet.RpcCode, args);

            // send it via broadcaster
            _graphcaster.SendGraphcast(packet);
        }

        public void SendRpcGossip(string rpcMethodName, params object[] args)
        {
            // get the packet from the npc
            // get gossip packet

            // send it to gossip component, that will buffers it until next gossip turn
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
            _rpcHandlers[rpcCode].DynamicInvoke(AtomSerializer.DeserializeDynamic(rpcCode, payload));
        }

        #region Test

        [Button]
        public void Send_RpcBenchmark()
        {
            SendRpcBroadcasted(nameof(Rpc_benchmark), controller.gameObject.name);
        }

        public void Rpc_benchmark(string sender)
        {
            Debug.Log("Received rpc from " + sender);
        }

        #endregion
    }
}
