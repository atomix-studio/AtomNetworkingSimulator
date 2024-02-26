using System;
using System.Reflection;

namespace Atom.DependencyProvider
{
    public partial class NodeComponentProvider
    {
        public class TypeInjectorHandler
        {
            private Type _contextType;
            private Type _fieldType;
            private DynamicMemberDelegateBinder _binder;

            public TypeInjectorHandler(Type context, FieldInfo fieldInfo)
            {
                _contextType = context;
                _fieldType = fieldInfo.FieldType;
                _binder = new DynamicMemberDelegateBinder();
                _binder.createFieldDelegatesAuto(fieldInfo);
            }

            public TypeInjectorHandler(Type context, PropertyInfo propertyInfo)
            {
                _contextType = context;
                _fieldType = propertyInfo.PropertyType;
                _binder = new DynamicMemberDelegateBinder();
                _binder.createPropertyDelegatesAuto(propertyInfo);
            }

            public void Inject(NodeComponentProvider provider, object instance)
            {
                _binder.setValueGeneric(instance, provider.Get(_fieldType, false));
            }

            public object Inject(object instance)
            {
                var dependency = DependencyProvider.getOrCreate(_fieldType, false);
                _binder.setValueGeneric(instance, dependency);
                return dependency;
            }
        }
    }
}
