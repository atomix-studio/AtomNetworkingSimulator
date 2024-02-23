using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public interface IBroadcastable 
    {
        public string broadcasterID { get; set; }   

        // identifier of the broadcaster
        public string broadcastID { get; set; }

        public INetworkPacket packet { get; }

        /// <summary>
        /// Returns a copy of the received packet but keeps the broadcast datas 
        /// </summary>
        /// <param name="received"></param>
        /// <returns></returns>
        public INetworkPacket GetForwardablePacket(INetworkPacket received);
    }
}
