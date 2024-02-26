using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Broadcasting.SelectiveForwarding
{
    /// <summary>
    /// Just a funny testing to see how much time a message could travel if broadcasted from 1 to 1 node to a specific node
    /// </summary>
    public class SelectiveForwardingComponent : MonoBehaviour  //, INodeComponent
    {
        public NodeEntity context { get; set; }
        [InjectComponent] private PacketRouter _packetRouter;
        [InjectComponent] private NetworkHandlingComponent _networkHandling;

        public void OnInitialize()
        {
            // throw new NotImplementedException();
            _packetRouter.RegisterPacketHandler(typeof(SinglecastPacket), HandleSinglecastReceivedAsync);
            _packetRouter.RegisterPacketHandler(typeof(SinglecastReceivedConfirmationPacket), null);
        }

        [Button]
        public void SendSelectiveForwardingToNodeByID(string nodeId)
        {
            ForwardSinglecastAsync(_networkHandling.Connections.ElementAt(0).Value.peerAdress, new SinglecastPacket(_networkHandling.LocalPeerInfo.peerAdress, _networkHandling.LocalPeerInfo.peerID, nodeId, 0));
        }


        protected async Task<bool> ForwardSinglecastAsync(string adress, SinglecastPacket singlecastPacket)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _packetRouter.SendRequest(adress, singlecastPacket, (response) =>
            { 
                // if no response, resend else d
                if(response == null)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                tcs.TrySetResult(true);
            });

            return await tcs.Task;
        }

        protected async void HandleSinglecastReceivedAsync(INetworkPacket networkPacket)
        {
            // send confirmation to sender to avoid him to call the packet to the next
            // if the confirmation fails, the packet will split in different instances
            var singlecast = (SinglecastPacket)networkPacket;
            var response = singlecast.GetResponsePacket(singlecast);
            _packetRouter.SendResponse(singlecast, response);

            // verify condition here
            // if match, return

            if(singlecast.targetID == _networkHandling.LocalPeerInfo.peerID)
            {
                Debug.LogError($"Found after {singlecast.cycles} cycles");

                // answer to first caller
                return;
            }

            while (true)
            {
                int nextIndex = BroadcastingHelpers.GetRandomConnectionIndexForBroadcast(_networkHandling.Connections, singlecast.casterID, singlecast.senderID, 6);

                var success = await ForwardSinglecastAsync(_networkHandling.Connections.ElementAt(nextIndex).Value.peerAdress, new SinglecastPacket(singlecast.casterAdress, singlecast.casterID, singlecast.targetID, singlecast.cycles));

                if (success)
                    break;
            }
        }
    }
}
