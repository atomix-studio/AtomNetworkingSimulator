using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    public class InjectionContextContainer
    {
        public Type InjectionContextType;
        public object InjectionContextInstance;
        public Dictionary<Type, object> injectedComponents = new Dictionary<Type, object>();

        public InjectionContextContainer(object injectionContextInstance)
        {
            InjectionContextType = injectionContextInstance.GetType();
            InjectionContextInstance = injectionContextInstance;
        }
    }
}
