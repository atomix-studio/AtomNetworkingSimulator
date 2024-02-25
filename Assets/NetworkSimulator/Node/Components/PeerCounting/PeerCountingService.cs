using Atom.CommunicationSystem;
using Atom.ComponentProvider;
using Atom.Components.PeerCounting;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Atom.Broadcasting
{
    public class PeerCountingService : MonoBehaviour, INodeComponent
    {
        [InjectComponent] private BroadcasterComponent _broadcaster;
        [InjectComponent] private PacketRouter _router;
        [SerializeField, ShowInInspector, ReadOnly] private int _responsesCount = 0;

        public NodeEntity context { get; set; }

        public void OnInitialize()
        {
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(PeerCountingBroadcastPacket), (onreceived) =>
            {
                var broadcastable = onreceived as IBroadcastablePacket;

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
