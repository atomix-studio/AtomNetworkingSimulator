using Atom.CommunicationSystem;
using System;

namespace Atom.Components.Gossip
{
    public interface IGossipPacket : IBroadcastablePacket
    {
        /// <summary>
        /// the very first moment the gossip request was broadcasted by its issuer
        /// </summary>
        public DateTime gossipStartedTime { get; set; }

        /// <summary>
        /// when a node starts to gossip about something, it initializes this ID that will be kept over  many broadcasts over the newtork
        /// </summary>
        public long gossipId { get; set; }

        /// <summary>
        /// version 0 is the original broadcast, this will increment each time a node gossip after receiving a gossip / at each round
        /// </summary>
        public int gossipGeneration { get; set; }

    }
}
