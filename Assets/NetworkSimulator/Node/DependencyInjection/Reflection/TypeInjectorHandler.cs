using System;
using System.Reflection;

namespace Atom.ComponentProvider
{
    public partial class NodeComponentProvider
    {
        public class TypeInjectorHandler
        {
            private Type _fieldType;
            private DynamicMemberDelegateBinder _binder;

            public TypeInjectorHandler(FieldInfo fieldInfo)
            {
                _fieldType = fieldInfo.FieldType;
                _binder = new DynamicMemberDelegateBinder();
                _binder.createFieldDelegatesAuto(fieldInfo);
            }

            public TypeInjectorHandler(PropertyInfo propertyInfo)
            {
                _fieldType = propertyInfo.PropertyType;
                _binder = new DynamicMemberDelegateBinder();
                _binder.createPropertyDelegatesAuto(propertyInfo);
            }

            public void Inject(NodeComponentProvider provider, object instance)
            {
                _binder.setValueGeneric(instance, provider.Get(_fieldType, false));
            }
        }
    }
}
