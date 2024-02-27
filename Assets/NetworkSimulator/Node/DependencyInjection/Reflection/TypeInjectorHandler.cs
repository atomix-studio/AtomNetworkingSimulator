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

            public void Inject(NodeComponentProvider provider, object instance)
            {
                _binder.setValueGeneric(instance, provider.Get(_reflectingType, false));
            }

            public object Inject(object instance)
            {
                var dependency = DependencyProvider.getOrCreate(_reflectingType, instance);
                _binder.setValueGeneric(instance, dependency);
                return dependency;
            }

            public object Inject(object instance, object masterContext)
            {
                var dependency = DependencyProvider.getOrCreate(_reflectingType, masterContext);
                _binder.setValueGeneric(instance, dependency);
                return dependency;
            }
        }
    }
}
