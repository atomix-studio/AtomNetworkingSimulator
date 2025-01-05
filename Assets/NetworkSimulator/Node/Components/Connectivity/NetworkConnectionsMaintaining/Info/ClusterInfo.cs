using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    [Serializable]
    public class ClusterInfo
    {
        public string ClusterName;

        public List<NodeEntity> BootNodes;
    }
}
