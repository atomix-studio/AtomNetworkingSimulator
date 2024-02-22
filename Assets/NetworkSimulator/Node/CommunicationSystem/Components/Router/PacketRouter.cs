﻿using Atom.CommunicationSystem;
using Atom.ComponentSystem;
using Atom.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public class PacketRouter : INodeUpdatableComponent
    {
        public NodeEntity context { get; set; }
        protected TransportLayerComponent transportLayer { get; set; }

        private string _peerId;
        protected string peerId

        {
            get
            {
                if (_peerId == null)
                {
                    _peerId = context.networkHandling.LocalPeerInfo.peerID;
                }
                return _peerId;
            }
        }

        private Dictionary<Type, short> _packetIdentifiers = new Dictionary<Type, short>();
        private Dictionary<short, Action<INetworkPacket>> _receivePacketHandlers = new Dictionary<short, Action<INetworkPacket>>();
        private Dictionary<long, INetworkPacketResponseAwaiter> _packetResponseAwaitersBuffer = new Dictionary<long, INetworkPacketResponseAwaiter>();
        private Action<INetworkPacket> _onReceiveExternal;

        private long _packetIdGenerator;
        protected long packetIdGenerator
        {
            get
            {
                return _packetIdGenerator++;
            }
        }

        private short _packetIdentifierGenerator = 0;

        public PacketRouter()
        {
        }

        public void OnInitialize()
        {
            // the callback is initialized and kept as a member to avoid runtime allocation 
            _onReceiveExternal = onReceivePacket;
            
            this.transportLayer = context.GetNodeComponent<TransportLayerComponent>();
            // we route the transport layer here
            // the transport layer will be an abstraction from this point so we need to ba able to hook up to many different implementations
            this.transportLayer.SetRoutingCallback(_onReceiveExternal);
        }

        public async void OnUpdate()
        {
            if (_packetResponseAwaitersBuffer.Count == 0)
                return;

            var now = DateTime.Now;
            for (int i = 0; i < _packetResponseAwaitersBuffer.Count; ++i)
            {
                var awaiter = _packetResponseAwaitersBuffer.ElementAt(i);

                if (awaiter.Value.createdTime.AddMilliseconds(awaiter.Value.timeout) > now)
                {
                    // on timed-out, we callback the resquest with NULL value so the service can 
                    // implement its logic on this assertion
                    awaiter.Value.responseCallback?.Invoke(null);

                    awaiter.Value.Dispose();
                    _packetResponseAwaitersBuffer.Remove(awaiter.Key);
                    i--;
                }
            }
        }

        public void RegisterPacketHandler(Type packetType, Action<INetworkPacket> packetReceiveHandler)
        {
            var packetIdentifier = _packetIdentifierGenerator++;

            _receivePacketHandlers.Add(packetIdentifier, packetReceiveHandler);

            if (_packetIdentifiers.ContainsKey(packetType))
                throw new Exception(packetType + " this " + context.gameObject);
            _packetIdentifiers.Add(packetType, packetIdentifier);
        }

        /// <summary>
        /// Sending should be done through the BROADCASTER COMPONENT wrapper
        /// </summary>
        /// <param name="targetAddress"></param>
        /// <param name="networkPacket"></param>
        public void Send(string targetAddress, INetworkPacket networkPacket)
        {
            onBeforeSendInternal(networkPacket);
            transportLayer.Send(targetAddress, networkPacket);
        }

        /// <summary>
        /// Sending should be done through the BROADCASTER COMPONENT wrapper
        /// </summary>
        /// <param name="targetAddress"></param>
        /// <param name="networkPacket"></param>
        public void SendResponse(IRespondable callingPacket, IResponse response)
        {
            onBeforeSendInternal(response as INetworkPacket);
            response.callerPacketUniqueId = (callingPacket as INetworkPacket).packetUniqueId;
            transportLayer.Send(callingPacket.senderAdress, (INetworkPacket)response);
        }

        /// <summary>
        /// Sending should be done through the BROADCASTER COMPONENT wrapper
        /// A request is a packet sent that will be awaiting for a response callback in its context
        /// </summary>
        /// <param name="targetAddress"></param>
        /// <param name="networkPacket"></param>
        /// <summary>      
        public void SendRequest(string targetAddress, INetworkPacket networkPacket, Action<INetworkPacket> responseCallback, int timeout_ms = 1000)
        {
            //transportLayer.SendPacket(target, networkPacket);
            onBeforeSendInternal(networkPacket);
            _packetResponseAwaitersBuffer.Add(networkPacket.packetUniqueId, new INetworkPacketResponseAwaiter(responseCallback, timeout_ms));
            transportLayer.Send(targetAddress, networkPacket);


            // transportLayer.Send
        }

        private void onBeforeSendInternal(INetworkPacket networkPacket)
        {
            networkPacket.packetUniqueId = packetIdGenerator;
            networkPacket.packetTypeIdentifier = _packetIdentifiers[networkPacket.GetType()];
            networkPacket.sentTime = DateTime.Now;
            networkPacket.senderID = peerId;

            if (networkPacket is IRespondable)
                (networkPacket as IRespondable).senderAdress = context.networkHandling.LocalPeerInfo.peerAdress;

            WorldSimulationManager._totalPacketSent++;
            WorldSimulationManager._totalPacketSentPerSecondCount++;
        }

        private void onReceivePacket(INetworkPacket networkPacket)
        {
            if (networkPacket is IResponse)
            {
                var callerId = (networkPacket as IResponse).callerPacketUniqueId;
                if (_packetResponseAwaitersBuffer.TryGetValue(callerId, out var awaiter))
                {
                    awaiter.responseCallback(networkPacket);
                    _packetResponseAwaitersBuffer.Remove(callerId);
                    awaiter.Dispose();
                    networkPacket.DisposePacket();
                    return;
                }

                // responses might be sent outside of a request awaiter (if implemented), so we have to check for a potential handler for the message outside of the awaiters
                if (_receivePacketHandlers.ContainsKey(networkPacket.packetTypeIdentifier))
                {
                    _receivePacketHandlers[networkPacket.packetTypeIdentifier].Invoke(networkPacket);
                    networkPacket.DisposePacket();
                    return;
                }

                networkPacket.DisposePacket();
                // else timed out, or something wrong in the logic
            }
            else
            {
                _receivePacketHandlers[networkPacket.packetTypeIdentifier].Invoke(networkPacket);
                networkPacket.DisposePacket();
            }
        }
    }
}
