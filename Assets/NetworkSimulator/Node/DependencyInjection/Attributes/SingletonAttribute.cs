using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    /// <summary>
    /// Mark a class as Singleton will limit is instanciation by the provider at 1 instance and let it be accessed statically
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SingletonAttribute : Attribute
    {
        // load on demand only. if false, singleton types will be instanced on intialization of the ComponentProvider
        public bool LazyLoad { get; set; } = false;

        public SingletonAttribute(bool lazyLoad = false)
        {
            LazyLoad = lazyLoad;
        }
    }
}
