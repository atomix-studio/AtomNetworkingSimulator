using Atom.CommunicationSystem;
using Atom.ComponentSystem;
using Atom.Services.PeerCounting;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Atom.BroadcastingProtocol
{
    public class PeerCountingService : MonoBehaviour, INodeComponent
    {
        [NodeComponentDependencyInject] private BroadcasterComponent _broadcaster;
        [NodeComponentDependencyInject] private PacketRouter _router;
        [SerializeField, ShowInInspector, ReadOnly] private int _responsesCount = 0;

        public NodeEntity context { get; set; }

        public void OnInitialize()
        {
            _router.RegisterPacketHandler(typeof(PeerCountingBroadcastPacket), (onreceived) =>
            {
                var broadcastable = onreceived as IBroadcastable;

                if (!_broadcaster.relayedBroadcastsBuffer.ContainsKey(broadcastable.broadcastID))
                {
                    var respondable = onreceived as IRespondable;
                    _broadcaster.Send(respondable.senderAdress, (INetworkPacket)respondable.GetResponsePacket(respondable));
                }

                _broadcaster.RelayBroadcast(broadcastable);
            });

            _router.RegisterPacketHandler(typeof(PeerCountingBroadcastResponsePacket), (onreceived) =>
            {
                _responsesCount++;
            });
        }

        public void OnUpdate()
        {
        }

        [Button]
        public void SendCountingBroadcast()
        {
            _responsesCount = 0;
            _broadcaster.SendBroadcast(new PeerCountingBroadcastPacket());
        }

    }
}
