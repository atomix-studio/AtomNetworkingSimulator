using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Broadcasting.Consensus
{
    [Serializable]
    public class RunningConsensusData
    {
        public bool hasExpired = false;
        public bool hasUpdate = false;
        public IConsensusPacket packet;
        public DateTime expiration_time;
    }
}
