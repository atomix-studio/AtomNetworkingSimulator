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
        public float ExpirationDelay;

        public DateTime expirationTime { get; set; }
        public bool hasExpired => expirationTime < DateTime.Now;

        public MinimumOutgoingEdge(PeerInfo innerFragmentNode, PeerInfo outerFragmentNode, long outerFragmentId, int outerFragmentLevel, float expirationDelay)
        {
            InnerFragmentNode = innerFragmentNode;
            OuterFragmentNode = outerFragmentNode;
            OuterFragmentId = outerFragmentId;
            OuterFragmentLevel = outerFragmentLevel;
            ExpirationDelay = expirationDelay;
            Refresh();
        }

        public void Refresh()
        {
            expirationTime = DateTime.Now.AddSeconds(ExpirationDelay);
        }
    }
}
