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
        public Type InjectionContextType { get; set; }
        public object InjectionContextInstance { get; set; }
        public Dictionary<Type, object> InjectedDependencies { get; set; } = new Dictionary<Type, object>();

        public InjectionContextInstanceContainer(object injectionContextInstance)
        {
            InjectionContextType = injectionContextInstance.GetType();
            InjectionContextInstance = injectionContextInstance;
        }
    }
}
