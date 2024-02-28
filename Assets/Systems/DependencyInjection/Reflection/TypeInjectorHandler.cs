using System;
using System.Reflection;

namespace Atom.DependencyProvider
{
    public partial class NodeComponentProvider
    {
        public class TypeInjectorHandler
        {
            private Type _contextType;
            private Type _reflectingType;
            private DynamicMemberDelegateBinder _binder;

            public Type reflectingType => _reflectingType;

            public TypeInjectorHandler(Type context, FieldInfo fieldInfo)
            {
                _contextType = context;
                _reflectingType = fieldInfo.FieldType;
                _binder = new DynamicMemberDelegateBinder();
                _binder.createFieldDelegatesAuto(fieldInfo);
            }

            public TypeInjectorHandler(Type context, PropertyInfo propertyInfo)
            {
                _contextType = context;
                _reflectingType = propertyInfo.PropertyType;
                _binder = new DynamicMemberDelegateBinder();
                _binder.createPropertyDelegatesAuto(propertyInfo);
            }
/*
            public void Inject(NodeComponentProvider provider, object instance)
            {
                _binder.setValueGeneric(instance, provider.Get(_reflectingType, false));
            }
*/
            /// <summary>
            /// container binded on the same instance as the injected
            /// </summary>
            /// <param name="instance"></param>
            /// <returns></returns>
            public object Inject(object instance)
            {
                var dependency = DependencyProvider.getOrCreate(_reflectingType, instance);
                _binder.setValueGeneric(instance, dependency);
                return dependency;
            }

            /// <summary>
            /// injected instance binded to an external/existing container
            /// </summary>
            /// <param name="instance"> The instance that the injection targets </param>
            /// <param name="container"> Where the dependencies have been instantiated and are kept. </param>
            /// <returns></returns>
            public object Inject(object instance, object container)
            {
                if(container == null)
                    return Inject(instance);

                var dependency = DependencyProvider.getOrCreate(_reflectingType, container);
                _binder.setValueGeneric(instance, dependency);
                return dependency;
            }
        }
    }
}
