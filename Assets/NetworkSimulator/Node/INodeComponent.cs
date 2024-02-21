using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.ComponentSystem
{
    public interface INodeComponent
    {
        public NodeEntity context { get; set; }
        public void OnInitialize();
    }
}
