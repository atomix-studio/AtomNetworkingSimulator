using Atom.CommunicationSystem;
using Atom.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.UIElements;
using UnityEngine;

namespace Atom.CommunicationSystem
{
    public abstract class AbstractNetworkPacket : INetworkPacket
    {
        #region Data set by router at send
        public short packetTypeIdentifier { get; set; }
        public long packetUniqueId { get; set; }
        public long senderID { get; set; }
        public DateTime sentTime { get; set; }
        #endregion

        #region Dispose implementation

        private bool _disposed = false;

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
