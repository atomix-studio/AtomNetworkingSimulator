using Atom.Serialization;
using System;

namespace Atom.CommunicationSystem
{
    public interface INetworkPacket 
    {
        public short packetTypeIdentifier { get; set; }
        public long packetUniqueId { get; set; }
        public long senderID { get; set; }    
        public DateTime sentTime { get; set; }

        /// <summary>
        ///  access to IDisposable dispose
        /// </summary>
        public void DisposePacket();

        public virtual int GetReceptionDelayMs()
        {
            return (DateTime.Now - sentTime).Milliseconds;
        }

        public virtual byte[] SerializePacket()
        {
            return AtomSerializer.SerializeGeneric(this);
        }

        public virtual INetworkPacket DeserializePacket(byte[] bytes)
        {
            return (INetworkPacket)AtomSerializer.DeserializeGeneric(this.GetType(), bytes);
        }
    }
}
