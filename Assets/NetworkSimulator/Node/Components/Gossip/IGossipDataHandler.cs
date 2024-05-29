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

    public interface IGossipDataHandler<T> : IGossipDataHandler where T : IBroadcastablePacket
    {
        public abstract void OnReceiveGossip(T data);

    }

}
