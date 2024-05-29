using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    /// <summary>
    /// A container object for a singleton that stores the parameters and the instance of the singleton.
    /// This is where the provider is keeping a reference to the singleton.
    /// </summary>
    public class SingletonContainer
    {
        public Type sType;
        public string prefabResourcePath;
        public bool isLazyLoad;
        public bool allowSingletonOverride;
        public bool dontDestroyOnLoad;
        public object Instance;

        public SingletonContainer(Type sType, bool isLazyLoad, bool allowSingletonOverride, bool dontDestroyOnLoad, string prefabResourcePath = null)
        {
            this.sType = sType;
            this.prefabResourcePath = prefabResourcePath;
            this.isLazyLoad = isLazyLoad;
            this.allowSingletonOverride = allowSingletonOverride;
            this.dontDestroyOnLoad = dontDestroyOnLoad; 
        }
    }
}
