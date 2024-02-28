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
        /// <summary>
        /// The singleton instance will be created when an InjectionContext requires its creation by marked a field of type of the singleton in its members.
        /// </summary>
        public bool LazyLoad { get; set; } = false;
        
        /// <summary>
        /// Allow the dependency provider to destroy existing instances of the singleton if more than one instance is found at initialization.
        /// </summary>
        public bool AllowSingletonOverride { get; set; } = false;

        /// <summary>
        /// (WIP) Instance of the singleton will be kept between scene changes
        /// </summary>
        public bool DontDestroyOnLoad { get; set; } = false;

        public SingletonAttribute(bool lazyLoad = false, bool allowSingletonOverride = false, bool dontDestroyOnLoad = false)
        {
            LazyLoad = lazyLoad;
            AllowSingletonOverride = allowSingletonOverride;
            DontDestroyOnLoad = dontDestroyOnLoad;
        }
    }
}
