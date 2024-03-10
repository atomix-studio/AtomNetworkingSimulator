using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    // when a node receive a take lead by another, if its id is higher, it will discard the cast, send a new TakeLead with his own ID, and send a direct discard to the previous requester
    public class DiscardTakeLeadPacket : AbstractNetworkPacket
    {
    }
}
