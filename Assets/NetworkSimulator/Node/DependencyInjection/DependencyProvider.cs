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
        private static bool _initialized = false;

        #region Assembly types
        private static List<Type> _assemblyTypes;
        #endregion

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

        #region Anonymous Dependencies
        /// An InjectionContext can declare required types instances that will be instanced at its initialization event if they are not referenced in any member as InjectDependency
        private static Dictionary<Type, List<Type>> _requiredDependenciesTypes;
        #endregion

        /// <summary>
        /// each instance of a class that is InjectionContext can be referenced in the injectionContextContainers, which will hold all the datas about injected components
        /// </summary>
        private static Dictionary<object, InjectionContextInstanceContainer> _injectionContextContainers;
        public static Dictionary<object, InjectionContextInstanceContainer> injectionContextContainers => _injectionContextContainers;

        private static Dictionary<Type, SingletonContainer> _singletonContainers;
        private static Dictionary<Type, object> _singletons;

        /// <summary>
        /// This dictionary holds the actual direct reference to the singleton instances after they have been created
        /// </summary>
        public static Dictionary<Type, object> Singletons => _singletons;


        private static Dictionary<Type, IDependencyCreatedCallbackHandler> _injectionContextDependencyCreatedCallback;
        private static List<object> _injectedDependenciesInstancesBuffer;

        static DependencyProvider()
        {
            internalInitialize();

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= ResetProviderData;
            EditorApplication.playModeStateChanged += ResetProviderData;
#endif
        }

        #region INIT
        [RuntimeInitializeOnLoadMethodAttribute]
        /// <summary>
        /// The dependency provider will analyze the types from the assembly and create all the InjectorHandlers 
        /// that will be used by the injection requests from the InjectionContext instances created during runtime
        /// </summary>
        private static void internalInitialize()
        {
            if (_initialized)
                return;

            Debug.Log("Initializing Dependency Provider");
            _initialized = true;

            _initializeCollections();

            // should it be able to handle classes from other assembly than base UnityCsharp ?
            _assemblyTypes = typeof(DependencyProvider).Assembly.GetTypes().ToList();
            var all_types = new List<Type>();
            all_types.AddRange(_assemblyTypes);

            // initializing the abstract types that are flagged as [InjectionContext]
            var injectableAbstractTypes = new Dictionary<Type, InjectionContextAttribute>();
            for (int i = 0; i < all_types.Count; ++i)
            {
                if (all_types[i].IsAbstract)
                {
                    foreach (var attr in all_types[i].GetCustomAttributes(true))
                    {
                        if (attr.GetType() == typeof(InjectionContextAttribute))
                        {
                            injectableAbstractTypes.Add(all_types[i], (InjectionContextAttribute)attr);
                            break;
                        }
                    }

                    all_types.RemoveAt(i);
                    i--;
                }
            }

            // each injectionContext type will have a handler with delegates to set all the InjectComponent flagged fields.
            _initializeInjectorHandlers(all_types, injectableAbstractTypes);

            _initializeSingletons(all_types);
        }

        private static void _initializeInjectorHandlers(List<Type> all_types, Dictionary<Type, InjectionContextAttribute> injectableAbstractTypes)
        {
            // iterating all the types of the assembly to generate the InjectorHandlers and the SingletonContainers
            for (int i = 0; i < all_types.Count; ++i)
            {
                if (tryAddInheritedFromInjectionContext(all_types[i], injectableAbstractTypes))
                    continue;

                var atts = all_types[i].GetCustomAttributes(true);

                foreach (var attribute in atts)
                {
                    // classes marked as injection context means that they can hold InjectComponent members that needs to be injected
                    if (attribute.GetType() == typeof(InjectionContextAttribute))
                    {
                        _injectionContextTypes.Add(all_types[i]);
                        _generateInjectorHandler(all_types[i], (InjectionContextAttribute)attribute);
                        break;
                    }
                }
            }
        }

        private static void _initializeSingletons(List<Type> all_types)
        {
            for (int i = 0; i < all_types.Count; ++i)
            {
                var atts = all_types[i].GetCustomAttributes(true);

                // Detecting singletons in a second time because singletons which are also InjectionContext will require their injectors to be
                // ready at the moment they are created (if not lazyLoaded)
                foreach (var attribute in atts)
                {
                    if (attribute.GetType() == typeof(SingletonAttribute))
                    {
                        var singleton_attribute = attribute as SingletonAttribute;

                        var container = new SingletonContainer(all_types[i], singleton_attribute.LazyLoad, singleton_attribute.AllowSingletonOverride);
                        _singletonContainers.Add(all_types[i], container);

                        if (!singleton_attribute.LazyLoad)
                        {
                            // create now
                            container.Instance = _findOrCreateSingleton(container);
                        }
                        // lazy loaded singletons will be instanced if an injection request is required by another InjectionContext
                        // to be noted, Singleton flagged classes can also be InjectionContext classes and will be handled as is in a separate flow.
                    }
                }
            }
        }

        private static void _initializeCollections()
        {
            _assemblyTypes = new List<Type>();
            _injectionContextDependencyCreatedCallback = new Dictionary<Type, IDependencyCreatedCallbackHandler>();
            _injectedDependenciesInstancesBuffer = new List<object>();
            _requiredDependenciesTypes = new Dictionary<Type, List<Type>>();
            // to be refactored in the container ?
            // keeping injectionContext type could be good 
            _injectionContextTypes = new List<Type>();
            _injectionContextContainers = new Dictionary<object, InjectionContextInstanceContainer>();
            _injectorHandlers = new Dictionary<Type, List<TypeInjectorHandler>>();
            _singletonContainers = new Dictionary<Type, SingletonContainer>();
            _singletons = new Dictionary<Type, object>();
        }

        private static void ResetProviderData(PlayModeStateChange obj)
        {
            // we juste reset all collections so all references will be garbage collected
            if (obj == PlayModeStateChange.ExitingPlayMode)
            {
                _initializeCollections();
                _initialized = false;
            }
        }

        #endregion

        #region PUBLIC

        public static void registerInjectionContextDependenciesAwakeCallback(Type injectionContextType, IDependencyCreatedCallbackHandler dependencyProviderCallbackHandler)
        {
            if (!_initialized)
                internalInitialize();

            _injectionContextDependencyCreatedCallback.Add(injectionContextType, dependencyProviderCallbackHandler);
        }
        /// <summary>
        /// Should be called at instanciation of an InjectionContext object
        /// </summary>
        /// <param name="injectionContext"></param>
        /// <exception cref="Exception"></exception>
        public static void injectDependencies(object injectionContext, object dependencyContainer = null) //, object masterContext = null
        {
            if (!_initialized)
                internalInitialize();

            var ctxtType = injectionContext.GetType();
            if (injectorHandlers.TryGetValue(ctxtType, out var injectors))
            {
                Debug.Log($"Start injecting in {ctxtType}");

                /*if(masterContext == null)
                {*/
                    // local variable to store currently generating dependencies without reallocating lists over and over
                    _injectedDependenciesInstancesBuffer.Clear();
                //}

                if (_requiredDependenciesTypes.TryGetValue(ctxtType, out var anonymousDependencies))
                {
                    for (int i = 0; i < anonymousDependencies.Count; ++i)
                    {
                        // the anonymous dependency means that there is no member in the context that requests its creation
                        // the instance is created by not actually injected anywhere
                        // BUT it will be initialized as others and ready for use in the context
                        // anonymous dependencies are forced within the InjectionContextAttribute parameter ForceInheritedTypesInjectionInContext or ForceRequiredTypesInjectionInContext

                        object dependency = null;
                        if (dependencyContainer != null)
                            dependency = getOrCreate(anonymousDependencies[i], dependencyContainer);
                        else
                            dependency  = getOrCreate(anonymousDependencies[i], injectionContext);

                        if (!_injectedDependenciesInstancesBuffer.Contains(dependency))
                            _injectedDependenciesInstancesBuffer.Add(dependency);
                    }
                }

                foreach (var injector in injectors)
                {
                    object injected = null;
                    if (dependencyContainer != null)
                        injected = injector.Inject(injectionContext, dependencyContainer);
                    else
                        injected = injector.Inject(injectionContext);

                    Debug.Log($"Injected {injected} in {ctxtType}");
/*
                    if (_injectorHandlers.ContainsKey(injected.GetType()))
                    {
                        Debug.Log($"Recursive indent to {injected.GetType()}");

                        if (masterContext != null)
                            injectDependencies(injected, masterContext);
                        else
                            injectDependencies(injected, injectionContextInstance);
                    }
*/
                    if (!_injectedDependenciesInstancesBuffer.Contains(injected))
                        _injectedDependenciesInstancesBuffer.Add(injected);
                }

                if (_injectionContextDependencyCreatedCallback.TryGetValue(ctxtType, out var handler))
                {
                    // very very work in progress
                    // basically what we do is notifying all the injected dependencies that they have been all initiated 
                    // we send the instance of the injectionContext within the callback
                    // from this point, any cross referencing between all injected dependencies from the context are possible
                    // so it is a good entry point for initialization in a complex system where services can use each other
                    for (int i = 0; i < _injectedDependenciesInstancesBuffer.Count; ++i)
                    {
                        handler.OnDependencyInjected(_injectedDependenciesInstancesBuffer[i]);
                    }
                }

            }
            else
            {
                throw new Exception($"No injectors found for the type {injectionContext.GetType()}");
            }
        }

        public static T getOrCreate<T>(object dependencyContainer) where T : class
        {
            return (T)getOrCreate(typeof(T), dependencyContainer);
        }

        public static object getOrCreate(Type componentType, object dependencyContainer)
        {
            if (_singletonContainers.TryGetValue(componentType, out var singletonInstance))
            {
                if (singletonInstance == null)
                    _findOrCreateSingleton(singletonInstance);
            }

            InjectionContextInstanceContainer container = null;
            if (!_injectionContextContainers.TryGetValue(dependencyContainer, out container))
            {
                container = new InjectionContextInstanceContainer(dependencyContainer.GetType());
                _injectionContextContainers.Add(dependencyContainer, container);
            }

            if (container.InjectedDependencies.TryGetValue(componentType, out var comp)) return comp;

            return _createObject(componentType, dependencyContainer, container);
        }
        #endregion


        /// <summary>
        /// If a type inherits from an abstract flagged as InjectionContextType, it will be added in the injection context types here
        /// </summary>
        /// <param name="current"></param>
        /// <param name="injectableAbstractTypes"></param>
        /// <returns></returns>
        private static bool tryAddInheritedFromInjectionContext(Type current, Dictionary<Type, InjectionContextAttribute> injectableAbstractTypes)
        {
            foreach (var abstractInjectableType in injectableAbstractTypes)
            {
                if (abstractInjectableType.Key.IsAssignableFrom(current))
                {
                    _injectionContextTypes.Add(current);
                    _generateInjectorHandler(current, abstractInjectableType.Value);
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

        private static void _generateInjectorHandler(Type type, InjectionContextAttribute icAttribute)
        {
            var has_required_types = false;
            // an injection context can declare an abstract type with ForceInheritedTypesInjectionInContext parameter
            // all types inheriting from ForceInheritedTypesInjectionInContext will be added as anonymous dependencies if an injector desn't exist for them
            // (meaning that there is no field tagged with InjectComponent
            if (icAttribute.ForceInheritedTypesInjectionInContext != null)
            {
                _requiredDependenciesTypes.Add(type, new List<Type>());
                has_required_types = true;

                var childrentypes = TypeHelpers.GetInheritingTypes(icAttribute.ForceInheritedTypesInjectionInContext, _assemblyTypes);
                for (int i = 0; i < childrentypes.Count; ++i)
                {
                    _requiredDependenciesTypes[type].Add(childrentypes[i]);
                }

            }
            else if (icAttribute.ForceRequiredTypesInjectionInContext != null)
            {
                _requiredDependenciesTypes.Add(type, new List<Type>());
                has_required_types = true;

                for (int i = 0; i < icAttribute.ForceRequiredTypesInjectionInContext.Length; ++i)
                {
                    _requiredDependenciesTypes[type].Add(icAttribute.ForceRequiredTypesInjectionInContext[i]);
                }
            }

            // creating a delegate to handle the injection of components in a given type
            // by doing this we do the reflection call one time per type only
            _injectorHandlers.Add(type, new List<TypeInjectorHandler>());

            // TODO buffering the reflection datas to be reusable for other instances in a static class with member delegate binder
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (has_required_types)
                {
                    if (_requiredDependenciesTypes[type].Contains(field.FieldType))
                    {
                        _injectorHandlers[type].Add(new TypeInjectorHandler(type, field));

                        // in this situation we don't event check for the attribute
                        // we remove the field.FieldType from the collection at this point because the injector will handle creation and assignation in the context
                        // all the eventual remaining types in  _requiredDependenciesTypes[type] will be instanced as anonymous dependencies
                        // and kept in the injectionContextInstance container
                        _requiredDependenciesTypes[type].Remove(field.FieldType);
                        continue;
                    }
                }

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
                if (has_required_types)
                {
                    if (_requiredDependenciesTypes[type].Contains(property.PropertyType))
                    {
                        _injectorHandlers[type].Add(new TypeInjectorHandler(type, property));

                        // in this situation we don't event check for the attribute
                        // we remove the field.FieldType from the collection at this point because the injector will handle creation and assignation in the context
                        // all the eventual remaining types in  _requiredDependenciesTypes[type] will be instanced as anonymous dependencies
                        // and kept in the injectionContextInstance container
                        _requiredDependenciesTypes[type].Remove(property.PropertyType);
                        continue;
                    }
                }

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
            container.InjectedDependencies.Add(componentType, newServiceInstance);

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
            var casted = context as Component;
            var newServiceInstance = casted.gameObject.GetComponent(componentType);

            if (newServiceInstance == null)
                newServiceInstance = casted.gameObject.AddComponent(componentType);

            if (newServiceInstance == null)
                throw new Exception("Components of type Monobehaviour should be placed on the gameobject before intialisation as the provider can't add dynamic components");

            container.InjectedDependencies.Add(componentType, newServiceInstance);

            return newServiceInstance;
        }

        private static object _findOrCreateSingleton(SingletonContainer singletonContainer, bool allowSingletonDependenciesInjection = true)
        {
            var created = _findOrCreateSingletonInternal(singletonContainer);
            Singletons.Add(singletonContainer.sType, created);

            // singleton that are created at runtime start will have to wait for all the injectors to be ready before
            if (allowSingletonDependenciesInjection)
            {
                // Singletons that are also an Injection Context are dependency injected automatically at creation 
                if (_injectorHandlers.ContainsKey(singletonContainer.sType))
                    injectDependencies(created);
            }

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
