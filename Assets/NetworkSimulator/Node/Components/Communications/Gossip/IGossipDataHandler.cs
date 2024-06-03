using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.Gossip
{
    /// <summary>
    /// Allows to handle gossip data filtering/relaying in a handling inherited class
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IGossipDataHandler
    {
    }

    public interface IGossipDataHandler<T> : IGossipDataHandler where T : IGossipPacket
    {        
        /// <summary>
        /// Callback when receiving a related gossip packet
        /// </summary>
        /// <param name="data"></param>
        public abstract void OnReceiveGossip(GossipComponent context, T data);

    }

}
