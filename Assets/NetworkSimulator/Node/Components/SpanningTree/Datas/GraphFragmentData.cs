using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;

namespace Atom.Components.GraphNetwork
{
    [Serializable]
    public class GraphFragmentData
    {

        public long FragmentID = -1; // PEER ID OF THE FRAGMENT LEAD
        public int FragmentLevel = 0;

        public List<long> OldFragmentIDs = new List<long>();

        public List<PeerInfo> FragmentMembers = new List<PeerInfo>();

        public MinimumOutgoingEdge MinimumOutgoingEdge = null;


        public GraphFragmentData(short fragmentLevel, long fragmentLeaderId)
        {
            FragmentLevel = fragmentLevel;
            FragmentID = fragmentLeaderId;
        }

    }
}
