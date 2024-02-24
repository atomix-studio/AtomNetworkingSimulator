using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public struct INetworkPacketResponseAwaiter 
    {
        public DateTime expirationTime;
        public Action<INetworkPacket> responseCallback;

        //private bool _disposed;

        public INetworkPacketResponseAwaiter(DateTime expirationTime, Action<INetworkPacket> responseCallback)
        {
            this.expirationTime = expirationTime;
            this.responseCallback = responseCallback;
        }
/*
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
        }*/

    }
}
