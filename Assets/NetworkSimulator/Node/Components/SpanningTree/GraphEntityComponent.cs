using Atom.CommunicationSystem;
using Atom.ComponentSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{    
    public class GraphEntityComponent : INodeComponent
    {
        public NodeEntity context { get ; set ; }

        public int GraphFragmentLevel { get; set; } = 0;
        public PeerInfo LocalGraphFragmentMaster { get; set; } = null;


        public void OnInitialize()
        {
        }
    }
}
