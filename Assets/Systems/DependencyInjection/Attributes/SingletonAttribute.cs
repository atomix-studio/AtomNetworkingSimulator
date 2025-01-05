using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    public enum CreationModes
    {
        FindOrInstantiate = 0,

        /// <summary>
        /// Singleton prefabs can be created and set in Unity's resource folder. 
        /// It allows to use Inspector parameters to define the singleton behaviour.
        /// If the provider don't find any prefab in the resources, he will simply Instantiate it on a new gameObject.
        /// </summary>
        FindOrInstantiateFromResourcesTemplate = 1,
    }

    /// <summary>
    /// Mark a class as Singleton will limit is instanciation by the provider at 1 instance and let it be accessed statically
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SingletonAttribute : Attribute
    {
        /// <summary>
        /// Define the way the provider will handle the retrieval/creation of a singleton.
        /// </summary>
        public CreationModes CreationMode { get; set; }

        /// <summary>
        /// If CreationModes == FindOrInstantiateFromResourcesTemplate, define the path where the provider will look for a prefab of the singleton object.
        /// </summary>
        public string PrefabResourcePath { get; set; }

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
            CreationMode = CreationModes.FindOrInstantiate;
            LazyLoad = lazyLoad;
            AllowSingletonOverride = allowSingletonOverride;
            DontDestroyOnLoad = dontDestroyOnLoad;
        }

        public SingletonAttribute(string prefabResourcePath, bool lazyLoad = false, bool allowSingletonOverride = false, bool dontDestroyOnLoad = false)
        {
            CreationMode = CreationModes.FindOrInstantiateFromResourcesTemplate;
            PrefabResourcePath = prefabResourcePath;
            LazyLoad = lazyLoad;
            AllowSingletonOverride = allowSingletonOverride;
            DontDestroyOnLoad = dontDestroyOnLoad;
        }
    }
}
