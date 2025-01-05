using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public abstract class AbstractBroadcastablePacket : IBroadcastablePacket, IClonablePacket
    {
        #region Data set by router at send
        public short packetTypeIdentifier { get; set; }
        public long packetUniqueId { get; set; }
        public long senderID { get; set; }
        public DateTime sentTime { get; set; }
        public long broadcasterID { get; set; }
        public long broadcastID { get; set; }

        #endregion
        public abstract INetworkPacket ClonePacket(INetworkPacket received);

        #region Dispose implementation

        private bool _disposed = false;

        /*protected AbstractBroadcastablePacket(short packetTypeIdentifier, long packetUniqueId, string senderID, DateTime sentTime, string broadcasterID, string broadcastID)
        {
            this.packetTypeIdentifier = packetTypeIdentifier;
            this.packetUniqueId = packetUniqueId;
            this.senderID = senderID;
            this.sentTime = sentTime;
            this.broadcasterID = broadcasterID;
            this.broadcastID = broadcastID;
        }*/

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

      
        #endregion

    }
}
