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
        public string peerID {  get; set; } 
        public string peerAdress { get; set; }  

        /// <summary>
        /// last avalaible ping data with the peer
        /// </summary>
        public float ping { get; set; }

        /// <summary>
        ///  form -100 to 100, an indice of trust that a node is begin attributed by the network over time
        ///  all new nodes begin at 0
        /// </summary>
        public float trust_coefficient { get; set; }    
    }
}
