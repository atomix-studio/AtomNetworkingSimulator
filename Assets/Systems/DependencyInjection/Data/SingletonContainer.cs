using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    public class SingletonContainer
    {
        public Type sType;
        public bool isLazyLoad;
        public bool allowSingletonOverride;
        public bool dontDestroyOnLoad;
        public object Instance;

        public SingletonContainer(Type sType, bool isLazyLoad, bool allowSingletonOverride, bool dontDestroyOnLoad)
        {
            this.sType = sType;
            this.isLazyLoad = isLazyLoad;
            this.allowSingletonOverride = allowSingletonOverride;
            this.dontDestroyOnLoad = dontDestroyOnLoad; 
        }
    }
}
