using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    [Serializable]
    /// <summary>
    /// Holds data of a peer (adress, lease, etc)
    /// </summary>
    public class PeerInfo
    {
        public PeerInfo() { }

        public PeerInfo(string senderID, string peerAdress)
        {
            this.peerID = senderID;
            this.peerAdress = peerAdress;
            this.last_updated = DateTime.Now;    
        }

        public string peerID {  get; set; } 
        public string peerAdress { get; set; }  

        /// <summary>
        /// last avalaible ping data with the peer
        /// </summary>
        public float ping { get; set; }

        public float score { get; set; }

        /// <summary>
        ///  form -100 to 100, an indice of trust that a node is begin attributed by the network over time
        ///  all new nodes begin at 0
        /// </summary>
        public float trust_coefficient { get; set; }    

        public DateTime last_updated { get; set; }

        public float UpdatePeerScore(float ping, int callersCount, int listennersCount)
        {
            // the more ping the less score
            float pingratio = ping;
            // the less connection the more score (balancing the pingratio) => new comers with low connections have more chances to get new connections
            float connectivity = (1f / (callersCount + listennersCount + 1)) * 100f;

            float score = connectivity / pingratio;

            this.ping = pingratio;
            this.score = score;
            this.last_updated = DateTime.UtcNow;
            return score;
        }

    }
}
