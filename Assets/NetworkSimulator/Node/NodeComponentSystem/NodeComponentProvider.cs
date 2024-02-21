﻿using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Atom.ComponentSystem
{
    /// <summary>
    /// Manager/factory for services in the Node system
    /// 
    /// </summary>
    public class NodeComponentProvider : MonoBehaviour
    {
        public Dictionary<Type, INodeComponent> _components;
        public Dictionary<Type, INodeUpdatableComponent> _updatableComponents;
        private NodeEntity _context;

        public void Initialize()
        {
            _components = new Dictionary<Type, INodeComponent>();
            _updatableComponents = new Dictionary<Type, INodeUpdatableComponent>();
            _context = gameObject.GetComponent<NodeEntity>();
            InitializeServices();
        }

        private void InitializeServices()
        {
            var iNodeComponentType = typeof(INodeComponent);
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => iNodeComponentType.IsAssignableFrom(p) && !p.IsAbstract);

            foreach (var type in types)
            {
                var inst = Get(type, false);

                var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    var attributes = field.CustomAttributes;
                    foreach (var attribute in attributes)
                    {
                        if (attribute.AttributeType == typeof(NodeComponentDependencyInjectAttribute))
                        {
                            var dependency = Get(field.FieldType);
                            field.SetValue(inst, dependency);
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
                        if (attribute.AttributeType == typeof(NodeComponentDependencyInjectAttribute))
                        {
                            var dependency = Get(property.PropertyType);
                            property.SetValue(inst, dependency);

                            break;
                        }
                    }
                }

                if (!_components.ContainsKey(type))
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
                    throw new Exception("Components of type Monobehaviour should be placed on the gameobject before intialisation as the provider can't add dynamic components");

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

        public INodeComponent Get<T>() where T : INodeComponent
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
