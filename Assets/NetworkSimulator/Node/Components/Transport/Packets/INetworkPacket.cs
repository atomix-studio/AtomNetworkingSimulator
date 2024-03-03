using System;

namespace Atom.CommunicationSystem
{
    public interface INetworkPacket 
    {
        public short packetTypeIdentifier { get; set; }
        public long packetUniqueId { get; set; }
        public string senderID { get; set; }    
        public DateTime sentTime { get; set; }

        /// <summary>
        ///  access to IDisposable dispose
        /// </summary>
        public void DisposePacket();

        public virtual int GetReceptionDelayMs()
        {
            return (DateTime.Now - sentTime).Milliseconds;
        }
    }
}
