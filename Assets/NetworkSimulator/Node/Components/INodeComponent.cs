using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    [InjectionContext]
    public interface INodeComponent : IDependencyService<NodeEntity>
    {
        //[InjectComponent] public NodeEntity context { get; set; }
        public void OnInitialize();
    }
}
