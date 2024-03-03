using Atom.Transport;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atom.CommunicationSystem
{
    public enum Protocol
    {
        HTTP,
        TCP,
    }

    public class NetworkPacket : IDisposable, INetworkPacket
    {
        public string senderID { get; set; }
        public DateTime sentTime { get; set; }
        public short packetTypeIdentifier { get; set; }
        public long packetUniqueId { get; set; }


        public NodeEntity Broadcaster;
        public int BroadcastID;

        public NodeEntity Sender;
        public Protocol Protocol;
        public int ID;
        public string Payload;
        public NodeEntity Target;

        private float _speed = 25;

        private static int _idGenerator = 0;
        private Vector3 _position;
        private Vector3 _startPosition;
        private bool _disposed = false;
        private Color _color;
        private TransportLayerComponent _transportLayer;

        public List<NodeEntity> _potentialPeers;

        public NetworkPacket(TransportLayerComponent transportLayer)
        {
            _transportLayer = transportLayer;
            _speed = _transportLayer.MessageSpeed;
        }

        public void Send(NodeEntity sender, NodeEntity target, string payload)
        {
            _position = sender.transform.position;
            _startPosition = _position;

            Sender = sender;
            Target = target;
            Payload = payload;
            ID = _idGenerator++;

            _color = Color.cyan;
        }

        public void SendBroadcast(NodeEntity broadcaster, NodeEntity sender, NodeEntity target, string payload, int broadcastID)
        {
            if (broadcastID == -1)
                BroadcastID = _idGenerator++;
            else
                BroadcastID = broadcastID;

            Broadcaster = broadcaster;
            Send(sender, target, payload);
        }

        public void OnUpdate()
        {
            if (Target != null)
            {
                var direction = Target.transform.position - _position;
                Debug.DrawLine(_startPosition, _position, _color);

                if (direction.magnitude < .1f)
                {
                    if (Target.gameObject.activeSelf)
                    {
                        WorldSimulationManager._totalPacketReceived++;
                        WorldSimulationManager._totalPacketReceivedPerSecondCount++;
                        Target.transportLayer.OnPacketReceived(this);
                    }

                    _transportLayer.travellingPackets.Remove(this);

                    Dispose();
                }
                else
                {
                    direction.Normalize();
                    _position += direction * Time.deltaTime * _speed;
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            _disposed = true;
        }

        public void DisposePacket()
        {
            Dispose();
        }
    }
}
