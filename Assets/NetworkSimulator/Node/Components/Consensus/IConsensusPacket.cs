using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Broadcasting.Consensus
{
    public interface IConsensusPacket : IClonablePacket, IBroadcastablePacket
    {        
        /// <summary>
        /// when a node starts a consensus request, it initializes this ID that will be kept over  many broadcasts over the newtork
        /// </summary>
        public string consensusId { get; set; }

        /// <summary>
        /// version 0 is the original broadcast
        /// </summary>
        public int consensusVersion { get; set; }

        /// <summary>
        /// when a node receive a consensus packet for the first time, it will add it in the collection of running consensuses, select a response/choice, and broadcast this new version
        /// </summary>
        /// <param name="packet"></param>
        public void Aggregate(IConsensusPacket packet);

        public void SelectChoice();
    }
}
