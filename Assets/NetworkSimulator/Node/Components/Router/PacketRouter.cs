using Atom.DependencyProvider;
using Atom.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Atom.CommunicationSystem
{
    public static class PacketRouterEventHandler
    {
     
    }
    public class PacketRouter : MonoBehaviour, INodeUpdatableComponent
    {
        public NodeEntity controller { get; set; }
        [Inject] public TransportLayerComponent transportLayer { get; set; }
        [Inject] public NetworkHandlingComponent networkHandling { get; set; }

        private string _peerId;
       
        private Action<INetworkPacket> _onReceiveExternal;
        private Dictionary<Type, short> _packetIdentifiers = new Dictionary<Type, short>();
        private Dictionary<short, Action<INetworkPacket>> _receivePacketHandlers = new Dictionary<short, Action<INetworkPacket>>();
        private Dictionary<long, INetworkPacketResponseAwaiter> _packetResponseAwaitersBuffer = new Dictionary<long, INetworkPacketResponseAwaiter>();


        private List<Func<INetworkPacket, bool>> _receivePacketMiddlewares = new List<Func<INetworkPacket, bool>>();
        private List<Func<INetworkPacket, bool>> _sendPacketMiddlewares = new List<Func<INetworkPacket, bool>>();

        private event Action<IResponse> _onResponseReceived;
        private event Action<INetworkPacket> _onPacketReceived;

        private long _packetIdGenerator;
        protected long packetIdGenerator
        {
            get
            {
                return _packetIdGenerator++;
            }
        }

        private short _packetIdentifierGenerator = 0;

        public bool IsSleeping = false;

        public PacketRouter()
        {
        }

        public void OnInitialize()
        {
            // the callback is initialized and kept as a member to avoid runtime allocation 
            _onReceiveExternal = onReceivePacket;

            // we route the transport layer here
            // the transport layer will be an abstraction from this point so we need to ba able to hook up to many different implementations
            this.transportLayer.SetRoutingCallback(_onReceiveExternal);
        }

        public void InitPeerAdress(string peerAdress)
        {
            _peerId = peerAdress;
        }

        #region Middlewares and events registering
        public void RegisterPacketSendingMiddleware(Func<INetworkPacket, bool> routingMiddleware)
        {
            if (_sendPacketMiddlewares == null)
                _sendPacketMiddlewares = new List<Func<INetworkPacket, bool>>();

            _sendPacketMiddlewares.Add(routingMiddleware);
        }

        public void UnregisterPacketSendingMiddleware(Func<INetworkPacket, bool> routingMiddleware)
        {
            if (_sendPacketMiddlewares == null)
                return;

            _sendPacketMiddlewares.Add(routingMiddleware);
        }

        public void RegisterPacketReceiveMiddleware(Func<INetworkPacket, bool> routingMiddleware)
        {
            if (_receivePacketMiddlewares == null)
                _receivePacketMiddlewares = new List<Func<INetworkPacket, bool>>();

            _receivePacketMiddlewares.Add(routingMiddleware);
        }

        public void UnregisterPacketReceiveMiddleware(Func<INetworkPacket, bool> routingMiddleware)
        {
            if (_receivePacketMiddlewares == null)
                return;

            _receivePacketMiddlewares.Add(routingMiddleware);
        }

        public void RegisterOnResponseReceivedCallback(Action<IResponse> onResponseReceivedCallback) => _onResponseReceived += onResponseReceivedCallback;
        public void UnregisterOnResponseReceivedCallback(Action<IResponse> onResponseReceivedCallback) => _onResponseReceived = onResponseReceivedCallback;
        public void RegisterOnPacketReceivedCallback(Action<INetworkPacket> onPacketReceivedCallback) => _onPacketReceived += onPacketReceivedCallback;
        public void UnregisterOnPacketReceivedCallback(Action<INetworkPacket> onPacketReceivedCallback) => _onPacketReceived -= onPacketReceivedCallback;

        #endregion
        public async void OnUpdate()
        {
            
        }

        void Update()
        {
            if (IsSleeping)
                return;

            if (_packetResponseAwaitersBuffer.Count == 0)
                return;

            var now = DateTime.Now;
            for (int i = 0; i < _packetResponseAwaitersBuffer.Count; ++i)
            {
                var awaiter = _packetResponseAwaitersBuffer.ElementAt(i);

                if (awaiter.Value.expirationTime < now)
                {
                    // on timed-out, we callback the resquest with NULL value so the service can 
                    // implement its logic on this assertion
                    awaiter.Value.responseCallback?.Invoke(null);
                    _packetResponseAwaitersBuffer.Remove(awaiter.Key);
                    i--;
                }
            }
        }

        public void RegisterPacketHandler(Type packetType, Action<INetworkPacket> packetReceiveHandler, bool broadcasterCall = false)
        {
            if (!broadcasterCall && TypeHelpers.ImplementsInterface<IBroadcastablePacket>(packetType))
            {
                Debug.LogError($"Broadcastable packets should always be registered from the broadcaster => Error with packet of type {packetType}.");
            }

            var packetIdentifier = _packetIdentifierGenerator++;

            _receivePacketHandlers.Add(packetIdentifier, packetReceiveHandler);

            if (_packetIdentifiers.ContainsKey(packetType))
                throw new Exception(packetType + " this " + controller.gameObject);
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
            onBeforeSendInternal(response.packet);
            response.callerPacketUniqueId = callingPacket.packet.packetUniqueId;
            transportLayer.Send(callingPacket.senderAdress, response.packet);
        }

        /// <summary>
        /// Sending should be done through the BROADCASTER COMPONENT wrapper
        /// A request is a packet sent that will be awaiting for a response callback in its context
        /// </summary>
        /// <param name="targetAddress"></param>
        /// <param name="networkPacket"></param>
        /// <summary>      
        public void SendRequest(string targetAddress, INetworkPacket networkPacket, Action<INetworkPacket> responseCallback, int timeout_ms = 50000)
        {
            //transportLayer.SendPacket(target, networkPacket);
            onBeforeSendInternal(networkPacket);
            _packetResponseAwaitersBuffer.Add(networkPacket.packetUniqueId, new INetworkPacketResponseAwaiter(DateTime.Now, DateTime.Now.AddMilliseconds(timeout_ms), responseCallback));
            transportLayer.Send(targetAddress, networkPacket);


            // transportLayer.Send
        }

        private void onBeforeSendInternal(INetworkPacket networkPacket)
        {
            networkPacket.packetUniqueId = packetIdGenerator;
            networkPacket.packetTypeIdentifier = _packetIdentifiers[networkPacket.GetType()];
            networkPacket.sentTime = DateTime.Now;
            networkPacket.senderID = _peerId;

            if (networkPacket is IRespondable)
                (networkPacket as IRespondable).senderAdress = networkHandling.LocalPeerInfo.peerAdress;

           /* // security here. 
            // it happens that broadcastable packet are forwared as multicast
            // if broadcasterID and broadcastID have been cloned or haven't been set, the relayed broadcast from nodes will be filled with string.Empty and the packet multicasted will encounter bugs.
            if (networkPacket is IBroadcastablePacket)
            {
                var brdcst = networkPacket as IBroadcastablePacket;
                brdcst.broadcasterID = networkHandling.LocalPeerInfo.peerAdress;
                brdcst.broadcastID = Guid.NewGuid().ToString();
            }*/
        }

        private async void onReceivePacket(INetworkPacket networkPacket)
        {
            if (IsSleeping)
                return;

            // middlewares can block the reception
            for(int i = 0; i < _receivePacketMiddlewares.Count; ++i)
            {
                if (!_receivePacketMiddlewares[i](networkPacket))
                    return;
            }

            _onPacketReceived?.Invoke(networkPacket);

            if (networkPacket is IResponse)
            {
                var resp = (IResponse)networkPacket;

                var callerId = resp.callerPacketUniqueId;
                if (_packetResponseAwaitersBuffer.TryGetValue(callerId, out var awaiter))
                {
                    resp.requestPing = (DateTime.Now - awaiter.creationTime).Milliseconds;
                    _onResponseReceived?.Invoke(resp);  

                    awaiter.responseCallback(networkPacket);
                    _packetResponseAwaitersBuffer.Remove(callerId);
                    return;
                }

                // responses might be sent outside of a request awaiter (if implemented), so we have to check for a potential handler for the message outside of the awaiters
                if (_receivePacketHandlers.ContainsKey(networkPacket.packetTypeIdentifier))
                {
                    Debug.Log($"{networkPacket.GetType()} has been received by {controller} out of a request range.");
                    _onResponseReceived?.Invoke(resp);

                    _receivePacketHandlers[networkPacket.packetTypeIdentifier]?.Invoke(networkPacket);
                    return;
                }
            }
            else
            {
                _receivePacketHandlers[networkPacket.packetTypeIdentifier].Invoke(networkPacket);
            }
        }
    }
}
