using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.ComponentProvider
{
    [Injectable]
    public interface INodeComponent
    {
        [InjectComponent] public NodeEntity context { get; set; }
        public void OnInitialize();
    }
}
