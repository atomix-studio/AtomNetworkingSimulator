using Atom.CommunicationSystem;
using Atom.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Broadcasting
{
    public static  class BroadcastingHelpers
    {
        public static int GetRandomConnectionIndexForBroadcast(Dictionary<string, PeerInfo> connections, string broadcasterId, string senderId, int breakCondition)
        {
            var count_break = 0;
            var index = 0;
            do
            {
                index = NodeRandom.Range(0, connections.Count);
                count_break++;

                if (count_break > breakCondition)
                    break;
            }

            // there is a probability that a node receive a broadcast from its callers that has been issued by a node contained in the callers view
            // so we don't want to send it back its message 
            while (connections.ElementAt(index).Value.peerID == broadcasterId
            || connections.ElementAt(index).Value.peerID == senderId);

            return index;
        }

    }
}
