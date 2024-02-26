using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    public struct SingletonContainer
    {
        public Type sType;
        public bool isLazyLoad;
        public object Instance;

        public SingletonContainer(Type sType, bool isLazyLoad, object instance)
        {
            this.sType = sType;
            this.isLazyLoad = isLazyLoad;
            Instance = instance;
        }
    }
}
