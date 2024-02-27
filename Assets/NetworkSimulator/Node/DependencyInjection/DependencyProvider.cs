using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Atom.DependencyProvider.NodeComponentProvider;

namespace Atom.DependencyProvider
{
    public static class DependencyProvider
    {
        #region Injection Context Types 
        private static List<Type> _injectionContextTypes;
        /// <summary>
        /// All the types flagged with [InjectionContextAttribute] in the current Assembly
        /// </summary>
        public static List<Type> InjectionContextTypes => _injectionContextTypes;
        #endregion

        #region Injector Handlers (delegate that handle the set values on the members flagged with InjectComponent in an InjectionContext
        private static Dictionary<Type, List<TypeInjectorHandler>> _injectorHandlers;
        /// <summary>
        /// injector handlers are created for each InjectionContext type. 
        /// They hold the delegates for the injection of the objects in the InjectionContext instances that will be instanced
        /// </summary>
        public static Dictionary<Type, List<TypeInjectorHandler>> injectorHandlers => _injectorHandlers;
        #endregion

        /// <summary>
        /// each instance of a class that is InjectionContext can be referenced in the injectionContextContainers, which will hold all the datas about injected components
        /// </summary>
        private static Dictionary<object, InjectionContextInstanceContainer> _injectionContextContainers;

        private static Dictionary<Type, SingletonContainer> _singletonContainers;
        private static Dictionary<Type, object> _singletons;

        /// <summary>
        /// This dictionary holds the actual direct reference to the singleton instances after they have been created
        /// </summary>
        public static Dictionary<Type, object> Singletons => _singletons;


        private static Dictionary<Type, IDependencyInjectionContextInitializedCallback> _injectionContextEndInitializedCallbacksDictionary;
        private static List<object> _injectedDependenciesInstancesBuffer;

        static DependencyProvider()
        {
            //internalInitialize();

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= ResetProviderData;
            EditorApplication.playModeStateChanged += ResetProviderData;
#endif
        }

        [RuntimeInitializeOnLoadMethodAttribute]
        /// <summary>
        /// The dependency provider will analyze the types from the assembly and create all the InjectorHandlers 
        /// that will be used by the injection requests from the InjectionContext instances created during runtime
        /// </summary>
        private static void internalInitialize()
        {
            Debug.Log("Initializing Dependency Provider");

            _initializeCollections();

            var all_types = typeof(DependencyProvider).Assembly.GetTypes().ToList();

            // initializing the abstract types that are flagged as [InjectionContext]
            var injectableAbstractTypes = new List<Type>();
            for (int i = 0; i < all_types.Count; ++i)
            {
                if (all_types[i].IsAbstract)
                {
                    foreach (var attr in all_types[i].GetCustomAttributes(true))
                    {
                        if (attr.GetType() == typeof(InjectionContextAttribute))
                        {
                            injectableAbstractTypes.Add(all_types[i]);
                            break;
                        }
                    }

                    all_types.RemoveAt(i);
                    i--;
                }
            }

            // iterating all the types of the assembly to generate the InjectorHandlers and the SingletonContainers
            for (int i = 0; i < all_types.Count; ++i)
            {
                if (tryAddInheritedFromInjectionContext(all_types[i], injectableAbstractTypes))
                    continue;

                var atts = all_types[i].GetCustomAttributes(true);

                foreach(var attribute in atts)
                {
                    // classes marked as injection context means that they can hold InjectComponent members that needs to be injected
                    if (attribute.GetType() == typeof(InjectionContextAttribute))
                    {
                        _injectionContextTypes.Add(all_types[i]);
                        _generateInjectorHandler(all_types[i]);
                    }
                }

                // Detecting singletons in a second time because singletons which are also InjectionContext will require their injectors to be
                // ready at the moment they are created (if not lazyLoaded)
                foreach (var attribute in atts)
                {
                    if (attribute.GetType() == typeof(SingletonAttribute))
                    {
                        var singleton_attribute = attribute as SingletonAttribute;

                        var container = new SingletonContainer(all_types[i], singleton_attribute.LazyLoad, singleton_attribute.AllowSingletonOverride);
                        if (!singleton_attribute.LazyLoad)
                        {
                            // create now
                            container.Instance = _findOrCreateSingleton(container);
                        }
                        // lazy loaded singletons will be instanced if an injection request is required by another InjectionContext
                        _singletonContainers.Add(all_types[i], container);
                        // to be noted, Singleton flagged classes can also be InjectionContext classes and will be handled as is in a separate flow.
                    }
                }
            }
        }

        private static void ResetProviderData(PlayModeStateChange obj)
        {
            // we juste reset all collections so all references will be garbage collected
            if (obj == PlayModeStateChange.ExitingPlayMode)
                _initializeCollections();
        }

        #region PUBLIC
        /// <summary>
        /// Should be called at instanciation of an InjectionContext object
        /// </summary>
        /// <param name="injectionContextInstance"></param>
        /// <exception cref="Exception"></exception>
        public static void injectDependencies(object injectionContextInstance)
        {
            var ctxtType = injectionContextInstance.GetType();
            if (injectorHandlers.TryGetValue(ctxtType, out var injectors))
            {
                // local variable to store currently generating dependencies without reallocating lists over and over
                _injectedDependenciesInstancesBuffer.Clear();
                foreach (var injector in injectors)
                {
                    _injectedDependenciesInstancesBuffer.Add(injector.Inject(injectionContextInstance));
                }

                if (_injectionContextEndInitializedCallbacksDictionary.TryGetValue(ctxtType, out var midd))
                {
                    // very very work in progress
                    // basically what we do is notifying all the injected dependencies that they have been all initiated 
                    // we send the instance of the injectionContext within the callback
                    // from this point, any cross referencing between all injected dependencies from the context are possible
                    // so it is a good entry point for initialization in a complex system where services can use each other
                    for (int i = 0; i < _injectedDependenciesInstancesBuffer.Count; ++i)
                    {
                        midd.OnInjectionContextInitialized(_injectedDependenciesInstancesBuffer[i]);
                    }
                }

            }
            else
            {
                throw new Exception($"No injectors found for the type {injectionContextInstance.GetType()}");
            }
        }

        public static T getOrCreate<T>(object context) where T : class
        {
            return (T)getOrCreate(typeof(T), context);
        }

        public static object getOrCreate(Type componentType, object context)
        {
            if (_singletonContainers.TryGetValue(componentType, out var singletonInstance))
            {
                if (singletonInstance == null)
                    _findOrCreateSingleton(singletonInstance);
            }

            InjectionContextInstanceContainer container = null;
            if (!_injectionContextContainers.TryGetValue(context, out container))
            {
                _injectionContextContainers.Add(context, new InjectionContextInstanceContainer(context.GetType()));
            }

            if (container.injectedDependencies.TryGetValue(componentType, out var comp)) return comp;
            return _createObject(componentType, context, container);
        }
        #endregion

        private static void _initializeCollections()
        {
            _injectionContextEndInitializedCallbacksDictionary = new Dictionary<Type, IDependencyInjectionContextInitializedCallback>();
            _injectedDependenciesInstancesBuffer = new List<object>();

            // to be refactored in the container ?
            // keeping injectionContext type could be good 
            _injectionContextTypes = new List<Type>();
            _injectionContextContainers = new Dictionary<object, InjectionContextInstanceContainer>();
            _injectorHandlers = new Dictionary<Type, List<TypeInjectorHandler>>();
            _singletonContainers = new Dictionary<Type, SingletonContainer>();
            _singletons = new Dictionary<Type, object>();
        }

        /// <summary>
        /// If a type inherits from an abstract flagged as InjectionContextType, it will be added in the injection context types here
        /// </summary>
        /// <param name="current"></param>
        /// <param name="injectableAbstractTypes"></param>
        /// <returns></returns>
        private static bool tryAddInheritedFromInjectionContext(Type current, List<Type> injectableAbstractTypes)
        {
            foreach (var abstractInjectableType in injectableAbstractTypes)
            {
                if (abstractInjectableType.IsAssignableFrom(current))
                {
                    _injectionContextTypes.Add(current);
                    _generateInjectorHandler(current);
                    return true;
                }
            }

            return false;
        }

        private static object _createObject(Type componentType, object context, InjectionContextInstanceContainer container)
        {
            if (componentType.IsSubclassOf(typeof(Component)))
            {
                return _createComponent(componentType, context, container);
            }
            else
            {
                return _createInstance(componentType, container);
            }
        }

        private static void _generateInjectorHandler(Type type)
        {
            // creating a delegate to handle the injection of components in a given type
            // by doing this we do the reflection call one time per type only
            _injectorHandlers.Add(type, new List<TypeInjectorHandler>());

            // TODO buffering the reflection datas to be reusable for other instances in a static class with member delegate binder
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var attributes = field.CustomAttributes;
                foreach (var attribute in attributes)
                {
                    if (attribute.AttributeType == typeof(InjectComponentAttribute))
                    {
                        _injectorHandlers[type].Add(new TypeInjectorHandler(type, field));
                        break;
                    }
                }
            }

            var properties = type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var property in properties)
            {
                var attributes = property.CustomAttributes;
                foreach (var attribute in attributes)
                {
                    if (attribute.AttributeType == typeof(InjectComponentAttribute))
                    {
                        _injectorHandlers[type].Add(new TypeInjectorHandler(type, property));
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// create pure c# class instance
        /// </summary>
        /// <param name="componentType"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        private static object _createInstance(Type componentType, InjectionContextInstanceContainer container)
        {
            var newServiceInstance = Activator.CreateInstance(componentType);
            container.injectedDependencies.Add(componentType, newServiceInstance);

            /*   if (newServiceInstance is INodeUpdatableComponent)
                   _updatableComponents.Add(typeof(T), newServiceInstance as INodeUpdatableComponent);

               newServiceInstance.OnInitialize();*/
            return newServiceInstance;
        }

        /// <summary>
        /// create unity component
        /// </summary>
        /// <param name="componentType"></param>
        /// <param name="context"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static object _createComponent(Type componentType, object context, InjectionContextInstanceContainer container)
        {
            var newServiceInstance = (context as Component).gameObject.GetComponent(componentType);

            if (newServiceInstance == null)
                newServiceInstance = (context as Component).gameObject.AddComponent(componentType);

            if (newServiceInstance == null)
                throw new Exception("Components of type Monobehaviour should be placed on the gameobject before intialisation as the provider can't add dynamic components");

            container.injectedDependencies.Add(componentType, newServiceInstance);

            return newServiceInstance;
        }

        private static object _findOrCreateSingleton(SingletonContainer singletonContainer)
        {
            var created = _findOrCreateSingletonInternal(singletonContainer);
            Singletons.Add(singletonContainer.sType, created);

            // Singletons that are also an Injection Context are dependency injected automatically at creation 
            if (_injectorHandlers.ContainsKey(singletonContainer.sType))
                injectDependencies(created);

            return created;
        }

        private static object _findOrCreateSingletonInternal(SingletonContainer singletonContainer)
        {
            if (singletonContainer.sType.IsSubclassOf(typeof(Component)))
            {
                var singletonInstances = GameObject.FindObjectsOfType(singletonContainer.sType, true);
                if (singletonInstances.Length > 1)
                {
                    if (!singletonContainer.allowSingletonOverride)
                        throw new Exception($"Multiple instances of the Singleton {singletonContainer.sType.Name} has been detected." +
                                $"You should have a look at your scene or allow AllowSingletonOverride in the singleton attribute.");

                    for (int i = 0; i < singletonInstances.Length; ++i)
                        Component.DestroyImmediate(singletonInstances[i]);

                    // nulling the array to force the creation of a new container + component below
                    singletonInstances = null;
                }

                if (singletonInstances.Length == 0 || singletonInstances == null)
                {
                    var singleton_gamobject = new GameObject("singleton_" + singletonContainer.sType.Name);
                    singletonContainer.Instance = singleton_gamobject.AddComponent(singletonContainer.sType);
                    return singletonContainer.Instance;
                }

                // singleton already exists in the scene and is alone as it should, we keep its reference in the container
                singletonContainer.Instance = singletonInstances[0];
                return singletonContainer.Instance;
            }
            else
            {
                var newServiceInstance = Activator.CreateInstance(singletonContainer.sType);
                singletonContainer.Instance = newServiceInstance;
                return singletonContainer.Instance;
            }
        }
    }
}
