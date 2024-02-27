using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    /// <summary>
    /// An object that holds the datas about the context of injection and its dependencies
    /// </summary>
    public class InjectionContextInstanceContainer
    {
        public Type InjectionContextType;
        public object InjectionContextInstance;
        public Dictionary<Type, object> injectedDependencies = new Dictionary<Type, object>();

        public InjectionContextInstanceContainer(object injectionContextInstance)
        {
            InjectionContextType = injectionContextInstance.GetType();
            InjectionContextInstance = injectionContextInstance;
        }
    }
}
