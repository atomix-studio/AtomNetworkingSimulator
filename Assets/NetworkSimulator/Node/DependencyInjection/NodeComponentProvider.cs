using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Atom.ComponentProvider
{
    /// <summary>
    /// Manager/factory for services in the Node system
    /// 
    /// TO DO REFACTORING TO GENERIFY THIS CRAP
    /// Needs to be extracted from monob madness and set as a context container somewhere in the system 
    /// 
    /// </summary>
    public partial class NodeComponentProvider : MonoBehaviour
    {
        private NodeEntity _context;
        private Dictionary<Type, INodeComponent> _components;
        private Dictionary<Type, INodeUpdatableComponent> _updatableComponents;

        private static Dictionary<Type, List<TypeInjectorHandler>> _injectorHandlers;

        private static Dictionary<Type, TypeInjectorHandler> _injectors;

        public void Initialize()
        {
            _injectorHandlers = new Dictionary<Type, List<TypeInjectorHandler>>();
            _components = new Dictionary<Type, INodeComponent>();
            _updatableComponents = new Dictionary<Type, INodeUpdatableComponent>();
            _context = gameObject.GetComponent<NodeEntity>();

            InitializeServices();
        }

        private void InitializeServices()
        {
            /*var iNodeComponentType = typeof(INodeComponent);
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => iNodeComponentType.IsAssignableFrom(p) && !p.IsAbstract);*/
            foreach (var type in ComponentProvider.InjectableTypes)
            {
                // creating a delegate to handle the injection of components in a given type
                // by doing this we do the reflection call one time per type only
                if (!_injectorHandlers.ContainsKey(type))
                {
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
                                /*var dependency = Get(field.FieldType);
                                field.SetValue(inst, dependency);*/
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
                                /* var dependency = Get(property.PropertyType);
                                 property.SetValue(inst, dependency);*/
                                _injectorHandlers[type].Add(new TypeInjectorHandler(property));
                                break;
                            }
                        }
                    }
                }

                var inst = Get(type, false);
                for (int j = 0; j < _injectorHandlers[type].Count; ++j)
                {
                    _injectorHandlers[type][j].Inject(this, inst);
                }
            }

            // 
            foreach (var type in ComponentProvider.InjectableTypes)
            {
                var inst = Get(type, false);
                inst.OnInitialize();
            }

            //TODO 
            // dependency injection by attribute on all classes marked with the [NodeComponentDependencyInjector] and [Inject] on field
        }

        public INodeComponent Get(Type type, bool callInit = true)
        {
            if (_components.ContainsKey(type)) return _components[type];

            if (type.IsSubclassOf(typeof(Component)))
            {
                var newServiceInstance = (INodeComponent)_context.gameObject.GetComponent(type);
                if (newServiceInstance == null)
                    newServiceInstance = (INodeComponent)_context.gameObject.AddComponent(type);

                newServiceInstance.context = _context;
                _components.Add(type, newServiceInstance);

                if (newServiceInstance is INodeUpdatableComponent)
                    _updatableComponents.Add(type, newServiceInstance as INodeUpdatableComponent);

                if (callInit)
                    newServiceInstance.OnInitialize();

                return newServiceInstance;
            }
            else
            {
                var newServiceInstance = (INodeComponent)Activator.CreateInstance(type);
                newServiceInstance.context = _context;
                _components.Add(type, newServiceInstance);

                if (newServiceInstance is INodeUpdatableComponent)
                    _updatableComponents.Add(type, newServiceInstance as INodeUpdatableComponent);

                if (callInit)
                    newServiceInstance.OnInitialize();

                return newServiceInstance;
            }
        }

        public T Get<T>() where T : INodeComponent
        {
            return (T)_get<T>();
        }

        public INodeComponent _get<T>() where T : INodeComponent
        {
            if (_components.ContainsKey(typeof(T))) return _components[typeof(T)];

            if (typeof(T).IsSubclassOf(typeof(Component)))
            {
                var newServiceInstance = (INodeComponent)_context.gameObject.GetComponent<T>();

                if (newServiceInstance == null)
                    throw new Exception("Components of type Monobehaviour should be placed on the gameobject before intialisation as the provider can't add dynamic components");

                newServiceInstance.context = _context;
                _components.Add(typeof(T), newServiceInstance);

                if (newServiceInstance is INodeUpdatableComponent)
                    _updatableComponents.Add(typeof(T), newServiceInstance as INodeUpdatableComponent);

                newServiceInstance.OnInitialize();
                return newServiceInstance;
            }
            else
            {
                var newServiceInstance = (T)Activator.CreateInstance(typeof(T));
                newServiceInstance.context = _context;
                _components.Add(typeof(T), newServiceInstance);

                if (newServiceInstance is INodeUpdatableComponent)
                    _updatableComponents.Add(typeof(T), newServiceInstance as INodeUpdatableComponent);

                newServiceInstance.OnInitialize();
                return newServiceInstance;
            }
        }

        private void Update()
        {
            foreach (var component in _updatableComponents)
                component.Value.OnUpdate();
        }
    }
}
