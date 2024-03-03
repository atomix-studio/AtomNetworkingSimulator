using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        #region Injector Handlers and Binders (delegate that handle the set values on the members flagged with InjectComponent in an InjectionContext

        /// <summary>
        /// Injector handler are the core of the provider function.
        /// For each type that is an injection context (aka contains [Inject] members), a dictionnary of TypeInjectorHandler is generated at initialization
        /// Whenever the Injection is called for the key type, all the injectors will be called to inject the values in the members of the context
        /// </summary>
        private static Dictionary<Type, Dictionary<Type, TypeInjectorHandler>> _injectorHandlers;

        /// <summary>
        /// TypeInjector definition override the GetOrCreate function for a given injected type
        /// note that typeInjectorDefinition are type-scoped, meaning that all Injector for the same type will follow the same rule
        // this probably will be parametrized in the InjectAttribute parameters in a future version.
        /// </summary>
        private static Dictionary<Type, ITypeInjectorDefinition> _typeInjectorDefinitions;
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

        private static List<object> _injectedDependenciesInstancesBuffer;
        private static HashSet<Type> _containerInjectionBuffer;

        static DependencyProvider()
        {
            _internalInitialize();

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= _resetProviderData;
            EditorApplication.playModeStateChanged += _resetProviderData;
#endif
        }

        #region INIT
        [RuntimeInitializeOnLoadMethodAttribute]
        /// <summary>
        /// The dependency provider will analyze the types from the assembly and create all the InjectorHandlers 
        /// that will be used by the injection requests from the InjectionContext instances created during runtime
        /// </summary>
        private static void _internalInitialize()
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

            _initializeTypeInjectorDefinitions();
        }

        private static void _initializeCollections()
        {
            _assemblyTypes = new List<Type>();
            _injectedDependenciesInstancesBuffer = new List<object>();
            _requiredDependenciesTypes = new Dictionary<Type, List<Type>>();
            _injectionContextTypes = new List<Type>();
            _injectionContextContainers = new Dictionary<object, InjectionContextInstanceContainer>();
            _injectorHandlers = new Dictionary<Type, Dictionary<Type, TypeInjectorHandler>>();
            _typeInjectorDefinitions = new Dictionary<Type, ITypeInjectorDefinition>();
            _singletonContainers = new Dictionary<Type, SingletonContainer>();
            _singletons = new Dictionary<Type, object>();
            _containerInjectionBuffer = new HashSet<Type>();
        }

        private static void _resetProviderData(PlayModeStateChange obj)
        {
            // we juste reset all collections so all references will be garbage collected
            if (obj == PlayModeStateChange.ExitingPlayMode)
            {
                _initializeCollections();
                _initialized = false;
            }
        }

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
            _injectorHandlers.Add(type, new Dictionary<Type, TypeInjectorHandler>());

            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                InjectAttribute inject_attribute = null;
                var attributes = field.GetCustomAttributes(true);
                foreach (var attribute in attributes)
                {
                    if (attribute.GetType() == typeof(InjectAttribute))
                    {
                        inject_attribute = attribute as InjectAttribute;
                        _injectorHandlers[type].Add(field.FieldType, new TypeInjectorHandler(type, field, inject_attribute));
                        break;
                    }
                }

                if (has_required_types && _requiredDependenciesTypes[type].Contains(field.FieldType))
                {
                    if (inject_attribute == null)
                        _injectorHandlers[type].Add(field.FieldType, new TypeInjectorHandler(type, field, null));

                    // we remove the field.FieldType from the collection at this point because the injector will handle creation and assignation in the context
                    // all the eventual remaining types in  _requiredDependenciesTypes[type] will be instanced as anonymous dependencies
                    // and kept in the injectionContextInstance container
                    _requiredDependenciesTypes[type].Remove(field.FieldType);
                }
            }

            var properties = type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var property in properties)
            {
                InjectAttribute inject_attribute = null;

                var attributes = property.GetCustomAttributes(true);
                foreach (var attribute in attributes)
                {
                    if (attribute.GetType() == typeof(InjectAttribute))
                    {
                        inject_attribute = attribute as InjectAttribute;
                        _injectorHandlers[type].Add(property.PropertyType, new TypeInjectorHandler(type, property, inject_attribute));
                        break;
                    }
                }

                if (has_required_types && _requiredDependenciesTypes[type].Contains(property.PropertyType))
                {
                    if (inject_attribute == null)
                        _injectorHandlers[type].Add(property.PropertyType, new TypeInjectorHandler(type, property, null));

                    // we remove the field.FieldType from the collection at this point because the injector will handle creation and assignation in the context
                    // all the eventual remaining types in  _requiredDependenciesTypes[type] will be instanced as anonymous dependencies
                    // and kept in the injectionContextInstance container
                    _requiredDependenciesTypes[type].Remove(property.PropertyType);
                }
            }
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

                        var container = new SingletonContainer(all_types[i], singleton_attribute.LazyLoad, singleton_attribute.AllowSingletonOverride, singleton_attribute.DontDestroyOnLoad);
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

        /// <summary>
        /// Type injector definitions defines override on the way the provider gets or create an instance of a type while injection occurs
        /// </summary>
        private static void _initializeTypeInjectorDefinitions()
        {
            foreach (var injectionContext in _injectorHandlers)
            {
                foreach (var injector in injectionContext.Value)
                {
                    // note that typeInjectorDefinition are type-scoped, meaning that all Injector for the same type will follow the same rule
                    // this probably will be parametrized in the InjectAttribute parameters in a future version.
                    if (!_typeInjectorDefinitions.ContainsKey(injector.Value.reflectingType))
                    {
                        if (injector.Value.injectAttribute?.TypeInjectorDefinition != null)
                        {
                            _typeInjectorDefinitions.Add(injector.Value.reflectingType, (ITypeInjectorDefinition)Activator.CreateInstance(injector.Value.injectAttribute.TypeInjectorDefinition.GetType()));
                        }
                    }
                }
            }
        }
        #endregion

        #region PUBLIC

        /// <summary>
        /// 
        /// </summary>
        /// <param name="injectionContext"> The instance of class/component that is the context of injection </param>
        /// <param name="dependencyContainer"> The container is the holder for all the dependencies injected in the injection context. 
        /// By default, the injection context is it's own container for non component types, and is the parent gameobject for unity's components. </param>
        /// <exception cref="Exception"></exception>
        public static void InjectDependencies(object injectionContext, object dependencyContainer = null, Action<List<object>> dependenciesInjectedCallback = null) //, object masterContext = null
        {
            _injectedDependenciesInstancesBuffer.Clear();
            _containerInjectionBuffer.Clear();
            _injectDependencies(injectionContext, dependencyContainer, false);
            dependenciesInjectedCallback?.Invoke(_injectedDependenciesInstancesBuffer);
        }

        public static object getOrCreate(Type componentType, object dependencyContainer)
        {
            // singletons are handled with a specific logic
            if (_singletonContainers.TryGetValue(componentType, out var singletonContainer))
            {
                if (singletonContainer == null)
                    return _findOrCreateSingleton(singletonContainer);

                return singletonContainer.Instance;
            }

            // the container is the object that holds dependencies reference for a given intance of an injection context
            InjectionContextInstanceContainer container = null;
            if (!_injectionContextContainers.TryGetValue(dependencyContainer, out container))
            {
                container = new InjectionContextInstanceContainer(dependencyContainer);
                _injectionContextContainers.Add(dependencyContainer, container);
            }

            // if the dependency already exist in that container we simply return the reference of it
            if (container.InjectedDependencies.TryGetValue(componentType, out var comp)) return comp;

            // if not we first check if the getOrCreate logic for the need-to-be-injected type has been overiden by a typeInjectorDefinition
            if (_typeInjectorDefinitions.TryGetValue(componentType, out var typeInjectorDefinition))
            {
                var obj = typeInjectorDefinition.GetOrCreate();
                container.InjectedDependencies.Add(componentType, obj);
                return obj;
            }

            return _createObject(componentType, container);
        }

        public static T getOrCreate<T>(object dependencyContainer) where T : class
        {
            return (T)getOrCreate(typeof(T), dependencyContainer);
        }
        #endregion

        #region Injection internal

        internal static void _injectDependencies(object injectionContext, object dependencyContainerOverride, bool recursiveCall = false) //, object masterContext = null
        {
            if (!_initialized)
                _internalInitialize();

            if (recursiveCall)
            {
                // avoid cycling while injecting recursively in a complex structure of 
                if (_containerInjectionBuffer.Contains(injectionContext.GetType()))
                {
                    //Debug.Log($"Type {injectionContext.GetType()} already initialized in container {dependencyContainerOverride} / context {injectionContext}");
                    return;
                }
                else
                {
                    _containerInjectionBuffer.Add(injectionContext.GetType());
                }
            }

            var ctxtType = injectionContext.GetType();
            if (_injectorHandlers.TryGetValue(ctxtType, out var injectors))
            {
                //Debug.Log($"Start injecting in {ctxtType}, recursive call : {recursiveCall}");

                if (!recursiveCall)
                {
                    _injectedDependenciesInstancesBuffer.Clear();
                    _containerInjectionBuffer.Clear();
                }

                if (_requiredDependenciesTypes.TryGetValue(ctxtType, out var anonymousDependencies))
                {
                    for (int i = 0; i < anonymousDependencies.Count; ++i)
                    {
                        // the anonymous dependency means that there is no member in the context that requests its creation
                        // the instance is created by not actually injected anywhere
                        // BUT it will be initialized as others and ready for use in the context
                        // anonymous dependencies are forced within the InjectionContextAttribute parameter ForceInheritedTypesInjectionInContext or ForceRequiredTypesInjectionInContext

                        object dependency = null;
                        if (dependencyContainerOverride != null)
                            dependency = getOrCreate(anonymousDependencies[i], dependencyContainerOverride);
                        else
                            dependency = getOrCreate(anonymousDependencies[i], injectionContext);

                        if (_injectorHandlers.ContainsKey(dependency.GetType()))
                        {
                            //Debug.Log($"Recursive indent to {injected.GetType()}");

                            if (dependencyContainerOverride != null)
                                _injectDependencies(dependency, dependencyContainerOverride, true);
                            else
                                _injectDependencies(dependency, injectionContext, true);
                        }

                        if (!_injectedDependenciesInstancesBuffer.Contains(dependency))
                            _injectedDependenciesInstancesBuffer.Add(dependency);
                    }
                }

                foreach (var injector in injectors)
                {
                    object injected = null;
                    if (dependencyContainerOverride != null)
                        injected = injector.Value.Inject(injectionContext, dependencyContainerOverride);
                    else
                        injected = injector.Value.Inject(injectionContext);

                    // if the type that we injected is also an InjectionContext, we will initialize it as well in the same container as the injection context.
                    // 
                    // but as circling references can exist that will lead to infinite loop, we add to keep a trace of what's have been instantiated in the container
                    // and to allow a type to be instantiated only once in this situation
                    // workaround can be passed with Attributes parameters 


                    if (_injectorHandlers.ContainsKey(injected.GetType()))
                    {
                        //Debug.Log($"Recursive indent to {injected.GetType()}");

                        if (dependencyContainerOverride != null)
                            _injectDependencies(injected, dependencyContainerOverride, true);
                        else
                            _injectDependencies(injected, injectionContext, true);
                    }

                    if (!_injectedDependenciesInstancesBuffer.Contains(injected))
                        _injectedDependenciesInstancesBuffer.Add(injected);
                }

                //Debug.LogError($"{ctxtType} initialized.");

                /* if (_injectionContextDependencyCreatedCallback.TryGetValue(ctxtType, out var handler))
                 {
                     // very very work in progress
                     // basically what we do is notifying all the injected dependencies that they have been all initiated 
                     // we send the instance of the injectionContext within the callback
                     // from this point, any cross referencing between all injected dependencies from the context are possible
                     // so it is a good entry point for initialization in a complex system where services can use each other
                     handler.OnDependencyInjected(_injectedDependenciesInstancesBuffer);
                     //_injectedDependenciesInstancesBuffer.Clear();
                 }*/

            }
            else
            {
                throw new Exception($"No injectors found for the type {injectionContext.GetType()}");
            }
        }

        #endregion

        #region Object creation

        private static object _createObject(Type componentType, InjectionContextInstanceContainer container)
        {
            if (componentType.IsSubclassOf(typeof(Component)))
            {
                return _createComponent(componentType, container);
            }
            else
            {
                return _createInstance(componentType, container);
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

            return newServiceInstance;
        }

        /// <summary>
        /// get, find or add unity component
        /// </summary>
        /// <param name="componentType"></param>
        /// <param name="context"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static object _createComponent(Type componentType, InjectionContextInstanceContainer container)
        {
            var casted = container.InjectionContextInstance as Component;

            if (casted.gameObject.TryGetComponent(componentType, out var comp))
            {
                container.InjectedDependencies.Add(componentType, comp);
                return comp;
            }

            if (_injectorHandlers.TryGetValue(container.InjectionContextType, out var handler) && handler.TryGetValue(componentType, out var typeInjector))
            {
                if (typeInjector.injectAttribute.InjectionOptions.HasFlag(InjectAttribute.InjectingOptions.AllowFindGameObject))
                {
                    comp = (Component)GameObject.FindObjectOfType(componentType);
                    if (comp != null)
                    {
                        container.InjectedDependencies.Add(componentType, comp);
                        return comp;
                    }
                }
            }

            comp = casted.gameObject.AddComponent(componentType);

            if (comp == null)
                throw new Exception($"Dependency provider error : Adding component {componentType} to {casted.gameObject} failed.");

            container.InjectedDependencies.Add(componentType, comp);

            return comp;
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
                    InjectDependencies(created);
            }

            // we initialize any static value coming in the singleton that is of the same type
            // but it won't be a good practice to keep doing that as the interest is to actually get an instance reference of the singleton where you need it WITHOUT gettint by Singleton.Instance type of call.
            var fields = singletonContainer.sType.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; ++i)
            {
                if (fields[i].FieldType == singletonContainer.sType)
                    fields[i].SetValue(created, created);
            }

            var properties = singletonContainer.sType.GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < properties.Length; ++i)
            {
                if (properties[i].PropertyType == singletonContainer.sType && properties[i].CanWrite)
                    properties[i].SetValue(created, created);
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

                    if (singletonContainer.dontDestroyOnLoad)
                        GameObject.DontDestroyOnLoad(singleton_gamobject);

                    return singletonContainer.Instance;
                }

                if (singletonContainer.dontDestroyOnLoad)
                    GameObject.DontDestroyOnLoad(((Component)singletonInstances[0]).gameObject);

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
        #endregion
    }
}
