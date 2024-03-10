using Atom.CommunicationSystem;
using System;

namespace Atom.Components.GraphNetwork
{
    public class MinimumOutgoingEdge
    {
        public PeerInfo InnerFragmentNode;
        public PeerInfo OuterFragmentNode;
        public long OuterFragmentId;
        public long OuterFragmentLevel;
        private DateTime _expirationTime { get; set; }
        public bool hasExpired => _expirationTime < DateTime.Now;

        public MinimumOutgoingEdge(PeerInfo innerFragmentNode, PeerInfo outerFragmentNode, long outerFragmentId, int outerFragmentLevel)
        {
            InnerFragmentNode = innerFragmentNode;
            OuterFragmentNode = outerFragmentNode;
            OuterFragmentId = outerFragmentId;
            Refresh();
        }

        public void Refresh()
        {
            _expirationTime = DateTime.Now.AddSeconds(6);
        }
    }
}
