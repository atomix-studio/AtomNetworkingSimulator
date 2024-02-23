using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace Atom.ComponentProvider
{
    public static class ComponentProvider
    {
        private static List<Type> _injectableTypes;
        public static List<Type> InjectableTypes => _injectableTypes;

        static ComponentProvider()
        {
            Debug.Log("Initialize component provider.");

            var iNodeComponentType = typeof(INodeComponent);
            var all_types = typeof(ComponentProvider).Assembly.GetTypes().ToList();
            _injectableTypes = new List<Type>();

            var injectableAbstractTypes = new List<Type>();
            for (int i = 0; i < all_types.Count; ++i)
            {
                if (all_types[i].IsAbstract)
                {
                    foreach(var attr in all_types[i].CustomAttributes)
                    {
                        if(attr.AttributeType == typeof(InjectableAttribute))
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
                if (checkInheritsFromAbstractInjectableType(all_types[i], injectableAbstractTypes))
                    continue;

                foreach (var attribute in all_types[i].CustomAttributes)
                {
                    if (attribute.AttributeType == typeof(InjectableAttribute) || attribute.AttributeType == typeof(SingletonAttribute))
                    {                        
                        _injectableTypes.Add(all_types[i]);

                        //_injectors.Add(all_types[i], new TypeInjectorHandler())
                        break;
                    }
                }
            }
        }

        private static bool checkInheritsFromAbstractInjectableType(Type current, List<Type> injectableAbstractTypes)
        {
            foreach (var abstractInjectableType in injectableAbstractTypes)
            {
                if (abstractInjectableType.IsAssignableFrom(current))
                {
                    _injectableTypes.Add(current);
                    return true;
                }
            }

            return false;
        }
    }
}
