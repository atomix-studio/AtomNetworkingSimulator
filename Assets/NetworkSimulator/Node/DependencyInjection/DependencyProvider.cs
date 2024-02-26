﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Atom.DependencyProvider.NodeComponentProvider;

namespace Atom.DependencyProvider
{
    public static class DependencyProvider
    {
        private static List<Type> _injectionContextTypes;
        public static List<Type> InjectionContextTypes => _injectionContextTypes;

        // injector handlers are created for each type that is INJECTION CONTEXT and not for each InjectComponent type
        private static Dictionary<Type, List<TypeInjectorHandler>> _injectorHandlers;
        public static Dictionary<Type, List<TypeInjectorHandler>> injectorHandlers => _injectorHandlers;

        // each instance of a class that is InjectionContext can be referenced in the injectionContextContainers, which will hold all the datas about injected components
        private static Dictionary<object, InjectionContextContainer> _injectionContextContainers;

        private static Dictionary<Type, object> _singletons;
        public static Dictionary<Type, object> Singletons => _singletons;

        static DependencyProvider()
        {
            Debug.Log("Initialize component provider.");

            var all_types = typeof(DependencyProvider).Assembly.GetTypes().ToList();
            _injectionContextTypes = new List<Type>();
            _injectorHandlers = new Dictionary<Type, List<TypeInjectorHandler>>();

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

            for (int i = 0; i < all_types.Count; ++i)
            {
                if (tryAddInheritedFromInjectionContext(all_types[i], injectableAbstractTypes))
                    continue;

                var atts = all_types[i].GetCustomAttributes(true);
                foreach (var attribute in atts)
                {
                    if (attribute.GetType() == typeof(SingletonAttribute))
                    {
                        var singleton_attribute = attribute as SingletonAttribute;

                        if (!singleton_attribute.LazyLoad)
                        {
                            // create now
                            _singletons.Add(all_types[i], new SingletonContainer(all_types[i], false, _createSingleton(all_types[i])));
                        }
                        else
                        {
                            _singletons.Add(all_types[i], new SingletonContainer(all_types[i], true, null));
                        }
                    }

                    // classes marked as injection context means that they can hold InjectComponent members that needs to be injected
                    if (attribute.GetType() == typeof(InjectionContextAttribute))
                    {
                        _injectionContextTypes.Add(all_types[i]);
                        generateInjectorHandler(all_types[i]);
                    }
                }
            }
        }

        private static void generateInjectorHandler(Type type)
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
                        _injectorHandlers[type].Add(new TypeInjectorHandler(field));
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
                        _injectorHandlers[type].Add(new TypeInjectorHandler(property));
                        break;
                    }
                }
            }
        }

        private static bool tryAddInheritedFromInjectionContext(Type current, List<Type> injectableAbstractTypes)
        {
            foreach (var abstractInjectableType in injectableAbstractTypes)
            {
                if (abstractInjectableType.IsAssignableFrom(current))
                {
                    _injectionContextTypes.Add(current);
                    generateInjectorHandler(current);
                    return true;
                }
            }

            return false;
        }

        // get a component for the instance of injectionContext represented by the context object
        public static T _getOrCreate<T>(object context) where T : class
        {
            InjectionContextContainer container = null;
            if (!_injectionContextContainers.TryGetValue(context, out container))
            {
                _injectionContextContainers.Add(context, new InjectionContextContainer(context.GetType()));
            }

            if (container.injectedComponents.TryGetValue(typeof(T), out var comp)) return (T)comp;

            if (typeof(T).IsSubclassOf(typeof(Component)))
            {
                var newServiceInstance = (context as Component).gameObject.GetComponent<T>();

                if (newServiceInstance == null)
                    throw new Exception("Components of type Monobehaviour should be placed on the gameobject before intialisation as the provider can't add dynamic components");

                container.injectedComponents.Add(typeof(T), newServiceInstance);

                // middleware

                /*if (newServiceInstance is INodeUpdatableComponent)
                    _updatableComponents.Add(typeof(T), newServiceInstance as INodeUpdatableComponent);

                newServiceInstance.OnInitialize();*/
                return newServiceInstance;
            }
            else
            {
                var newServiceInstance = (T)Activator.CreateInstance(typeof(T));
                container.injectedComponents.Add(typeof(T), newServiceInstance);

                // middleware

             /*   if (newServiceInstance is INodeUpdatableComponent)
                    _updatableComponents.Add(typeof(T), newServiceInstance as INodeUpdatableComponent);

                newServiceInstance.OnInitialize();*/
                return newServiceInstance;
            }
        }

        public static object _getOrCreate(Type componentType, object context)
        {
            InjectionContextContainer container = null;
            if (!_injectionContextContainers.TryGetValue(context, out container))
            {
                _injectionContextContainers.Add(context, new InjectionContextContainer(context.GetType()));
            }

            if (container.injectedComponents.TryGetValue(componentType, out var comp)) return comp;

            if (componentType.IsSubclassOf(typeof(Component)))
            {
                return _createComponent(componentType, context, container);
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
        private static object _createInstance(Type componentType, InjectionContextContainer container)
        {
            var newServiceInstance = Activator.CreateInstance(componentType);
            container.injectedComponents.Add(componentType, newServiceInstance);

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
        private static object _createComponent(Type componentType, object context, InjectionContextContainer container)
        {
            var newServiceInstance = (context as Component).gameObject.GetComponent(componentType);
            if (newServiceInstance == null)
                newServiceInstance = (context as Component).gameObject.AddComponent(componentType);


            if (newServiceInstance == null)
                throw new Exception("Components of type Monobehaviour should be placed on the gameobject before intialisation as the provider can't add dynamic components");

            container.injectedComponents.Add(componentType, newServiceInstance);

            /*if (newServiceInstance is INodeUpdatableComponent)
                _updatableComponents.Add(typeof(T), newServiceInstance as INodeUpdatableComponent);

            newServiceInstance.OnInitialize();*/
            return newServiceInstance;
        }

        private static object _createSingleton(Type componentType)
        {
            InjectionContextContainer container = new InjectionContextContainer(componentType);
            var holdergo = new GameObject(componentType.Name);
            _injectionContextContainers.Add(holdergo, container);

            if (componentType.IsSubclassOf(typeof(Component)))
            {

                return _createComponent(componentType, holdergo, container);    
            }
            else
            {
                var newServiceInstance = Activator.CreateInstance(componentType);
                container.injectedComponents.Add(componentType, newServiceInstance);

                return newServiceInstance;
            }
        }
    }
}