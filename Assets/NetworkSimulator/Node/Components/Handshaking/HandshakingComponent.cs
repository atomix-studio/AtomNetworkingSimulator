using Atom.CommunicationSystem;
using Atom.ComponentProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.Handshaking
{
    /// <summary>
    /// Handshaking component is used to get basic data about a peer (ping, number of connections, etc..) and to ensure the node is responding
    /// </summary>
    public class HandshakingComponent : INodeComponent
    {
        public NodeEntity context { get; set; }
        [InjectComponent] private PacketRouter _packetRouter;

        public void OnInitialize()
        {
            _packetRouter.RegisterPacketHandler(typeof(HandshakePacket), (packet) =>
            {
                var respondable = packet as IRespondable;
                var response = (HandshakeResponsePacket)(respondable.GetResponsePacket(respondable).packet);
                //response.networkInfoCallersCount = (byte)context.networkHandling.Callers.Count;
                response.networkInfoListennersCount = (byte)context.networkHandling.Connections.Count;

                _packetRouter.SendResponse(respondable, response);
            });
            _packetRouter.RegisterPacketHandler(typeof(HandshakeResponsePacket), null);
        }

        public async Task<HandshakeResponsePacket> GetHandshakeAsync(PeerInfo peer)
        {
            var taskCompletionSource = new TaskCompletionSource<HandshakeResponsePacket>();
            //var cts = new CancellationTokenSource(99999);

            var sentTime = DateTime.Now;
            _packetRouter.SendRequest(peer.peerAdress, new HandshakePacket(), (response) =>
            {
                var handshakeResponse = (HandshakeResponsePacket)response;
                peer.ping = (DateTime.Now - sentTime).Milliseconds;

                if (taskCompletionSource.Task.IsCanceled)
                    return;

                taskCompletionSource.SetResult(handshakeResponse);
            });

           /* Task completedTask = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(99999, cts.Token));

            if (completedTask == taskCompletionSource.Task)
            {
                // Task completed within timeout
                return await taskCompletionSource.Task;
            }
            else
            {
                // Task timed out
                try
                {
                    taskCompletionSource.TrySetCanceled(cts.Token); // If task can be canceled
                }
                catch (Exception)
                {
                    // Handle cancellation exception
                }
            }

            return null;*/

            return await taskCompletionSource.Task;    
        }

        public async Task<float> GetPingAsync(NodeEntity nodeEntity)
        {
            var taskCompletionSource = new TaskCompletionSource<float>();

            var sentTime = DateTime.Now;
            _packetRouter.SendRequest(nodeEntity.name, new HandshakePacket(), (response) =>
            {
                float ping = (DateTime.Now - sentTime).Milliseconds;
                taskCompletionSource.SetResult(ping);
            });

            return await taskCompletionSource.Task;
        }
    }
}
