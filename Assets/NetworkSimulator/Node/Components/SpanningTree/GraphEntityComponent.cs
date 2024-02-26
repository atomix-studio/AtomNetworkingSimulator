using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Components.GraphNetwork
{
    [Serializable]
    public class GraphFragmentData
    {
        public int FragmentLevel { get; set; } = 0;
        public string FragmentLeaderId { get; set; } = string.Empty;
    }

    [Serializable]
    public class GraphFragmentLeaderData
    {

    }

    public class GraphEntityComponent : INodeComponent
    {
        public NodeEntity context { get ; set ; }
        [InjectComponent] private BroadcasterComponent _broadcaster;

        public void OnInitialize()
        {

        }
    }
}
