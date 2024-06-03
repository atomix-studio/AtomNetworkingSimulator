using Atom.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public interface IResponse
    {
        public long callerPacketUniqueId { get; set; }
        public INetworkPacket packet { get; }

        /// <summary>
        /// this field is filled by the router juste before it handles the calling back in the request context of the reponse. 
        /// It allows components to get this data without recomputing it everytime
        /// ping is at the core of the connectivity of the system as it is part of the way we compute connection 'weight' or 'score'
        /// </summary>
        [SerializerIgnore] public int requestPing { get; set; }
    }
}
