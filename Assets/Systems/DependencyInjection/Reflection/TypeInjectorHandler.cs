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
            /// <summary>
            /// reference to the inject attribute from the injectionContext field represented by this injector
            /// </summary>
            private InjectAttribute _injectAttribute;

            /// <summary>
            /// The delegate binder generates delegate to set/get values from the reflection.
            /// The binder takes an instance of the _reflectingType as parameter, so it's reusable without getting through a reflection call again
            /// </summary>
            private DynamicMemberDelegateBinder _binder;

            public Type reflectingType => _reflectingType;

            /// <summary>
            /// Warning, can be null in the case of required/anonymous dependencies 
            /// </summary>
            public InjectAttribute injectAttribute => _injectAttribute;

            public TypeInjectorHandler(Type context, FieldInfo fieldInfo, InjectAttribute injectAttribute)
            {
                _contextType = context;
                _reflectingType = fieldInfo.FieldType;
                _injectAttribute = injectAttribute;
                _binder = new DynamicMemberDelegateBinder();
                _binder.createFieldDelegatesAuto(fieldInfo);
            }

            public TypeInjectorHandler(Type context, PropertyInfo propertyInfo, InjectAttribute injectAttribute)
            {
                _contextType = context;
                _reflectingType = propertyInfo.PropertyType;
                _injectAttribute = injectAttribute;
                _binder = new DynamicMemberDelegateBinder();
                _binder.createPropertyDelegatesAuto(propertyInfo);
            }

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
