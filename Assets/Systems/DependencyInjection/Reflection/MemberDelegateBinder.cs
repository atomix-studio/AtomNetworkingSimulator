using System;
using System.Reflection;

namespace Atom.DependencyProvider
{
    public enum ReflectedMemberDelegateAttributeType
    {
        Field,
        Property,
        Method,
    }

    /// <summary>
    /// Classe abstraite permettant de binder des délégués via reflection sur des propriétés, des champs ou des méthodes
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MemberDelegateBinder<T>
    {
        private const string _int16 = "System.Int16";
        private const string _uint16 = "System.UInt16";
        private const string _int32 = "System.Int32";
        private const string _uint32 = "System.UInt32";
        private const string _int64 = "System.Int64";
        private const string _uint64 = "System.UInt64";
        private const string _byte = "System.Byte";
        private const string _sbyte = "System.SByte";
        private const string _float = "System.Single";
        private const string _decimal = "System.Decimal";
        private const string _double = "System.Double";
        private const string _char = "System.Char";
        private const string _bool = "System.Boolean";
        private const string _string = "System.String";
        private const string _enum = "System.Enum";
        private const string _object = "System.Object";
        private const string _dateTime = "System.DateTime";

        /// <summary>
        /// Fiedl, Property or Method ?
        /// </summary>
        public ReflectedMemberDelegateAttributeType MemberAttributeType;

        /// <summary>
        /// Name of the field/property/method
        /// </summary>
        public string MemberName;
        /// <summary>
        /// Type of the field/property/method
        /// </summary>
        public string MemberType;

        /// <summary>
        /// If binded to an instance
        /// </summary>
        public T BindedInstance;

        /// <summary>
        /// To get the field or the property value (if the property has a getter)
        /// For methods, only getter will be used
        /// </summary>
        public Delegate Getter;

        /// <summary>
        /// To set the field or the property value (if the property has a setter)
        /// </summary>
        public Delegate Setter;

        public void createFieldDelegates<K, J>(FieldInfo fieldInfo)
        {
            try
            {
                MemberAttributeType = ReflectedMemberDelegateAttributeType.Field;
                MemberName = fieldInfo.Name;

                if (MemberType == string.Empty || MemberType == null)
                {
                    if (fieldInfo.FieldType == typeof(string))
                    {
                        MemberType = _string;
                    }
                    else if (fieldInfo.FieldType.IsEnum)
                    {
                        MemberType = _enum;
                    }
                    else if (fieldInfo.FieldType.IsPrimitive)
                    {
                        MemberType = fieldInfo.FieldType.ToString();
                    }
                    else if (fieldInfo.FieldType == typeof(DateTime))
                    {

                        MemberType = _dateTime;
                    }
                    else
                    {
                        MemberType = _object;
                    }
                }


                Getter = DelegateHelper.CreateFieldGetter<K, J>(fieldInfo);
                Setter = DelegateHelper.CreateFieldSetter<K, J>(fieldInfo);
            }
            catch (Exception e)
            {
                throw new Exception($"Creation of delegate on property {fieldInfo.Name}, {BindedInstance?.GetType()} failed. => {e}");
            }
        }

        public void createFieldDelegatesAuto(FieldInfo fieldInfo)
        {
            MemberAttributeType = ReflectedMemberDelegateAttributeType.Field;

            if (fieldInfo.FieldType == typeof(string))
            {
                MemberName = fieldInfo.Name;
                MemberType = _string;
                createFieldDelegates<T, string>(fieldInfo);
            }
            else if (fieldInfo.FieldType.IsEnum)
            {
                MemberName = fieldInfo.Name;
                MemberType = _enum;
                createFieldDelegates<T, int>(fieldInfo);
            }
            else if (fieldInfo.FieldType.IsPrimitive)
            {
                MemberName = fieldInfo.Name;
                MemberType = fieldInfo.FieldType.ToString();
                switch (MemberType)
                {
                    case _int32:
                        createFieldDelegates<T, int>(fieldInfo);
                        break;
                    case _float:
                        createFieldDelegates<T, float>(fieldInfo);
                        break;
                    case _bool:
                        createFieldDelegates<T, bool>(fieldInfo);
                        break;
                    case _string:
                        createFieldDelegates<T, string>(fieldInfo);
                        break;
                    case _int16:
                        createFieldDelegates<T, short>(fieldInfo);
                        break;
                    case _int64:
                        createFieldDelegates<T, long>(fieldInfo);
                        break;
                    case _byte:
                        createFieldDelegates<T, byte>(fieldInfo);
                        break;
                    case _double:
                        createFieldDelegates<T, double>(fieldInfo);
                        break;
                    case _object:
                        createFieldDelegates<T, object>(fieldInfo);
                        break;
                }
            }
            else if (fieldInfo.FieldType == typeof(DateTime))
            {

                MemberName = fieldInfo.Name;
                MemberType = _dateTime;
                createFieldDelegates<T, DateTime>(fieldInfo);
            }
            else
            {
                MemberName = fieldInfo.Name;
                MemberType = _object;
                createFieldDelegates<T, object>(fieldInfo);
            }
            /*

                        var is_enum = fieldInfo.FieldType.IsEnum;
                        if (fieldInfo.FieldType.IsPrimitive || is_enum)
                        {
                            MemberName = fieldInfo.Name;
                            MemberType = is_enum ? _enum : fieldInfo.FieldType.ToString();
                            switch (MemberType)
                            {
                                case _enum:
                                case _int32:
                                    createFieldDelegates<T, int>(fieldInfo);
                                    break;
                                case _float:
                                    createFieldDelegates<T, float>(fieldInfo);
                                    break;
                                case _bool:
                                    createFieldDelegates<T, bool>(fieldInfo);
                                    break;
                                case _string:
                                    createFieldDelegates<T, string>(fieldInfo);
                                    break;
                                case _int16:
                                    createFieldDelegates<T, short>(fieldInfo);
                                    break;
                                case _int64:
                                    createFieldDelegates<T, long>(fieldInfo);
                                    break;
                                case _byte:
                                    createFieldDelegates<T, byte>(fieldInfo);
                                    break;
                                case _double:
                                    createFieldDelegates<T, double>(fieldInfo);
                                    break;
                                case _object:
                                    createFieldDelegates<T, object>(fieldInfo);
                                    break;

                            }
                        }
                        else
                        {
                            MemberName = fieldInfo.Name;
                            MemberType = _object;
                            createFieldDelegates<T, object>(fieldInfo);
                        }*/
        }

        public void createPropertyDelegates<K, J>(PropertyInfo propertyInfo)
        {
            try
            {
                MemberAttributeType = ReflectedMemberDelegateAttributeType.Property;

                if (MemberType == string.Empty || MemberType == null)
                {
                    if (propertyInfo.PropertyType == typeof(string))
                    {
                        MemberType = _string;
                    }
                    else if (propertyInfo.PropertyType.IsEnum)
                    {
                        MemberType = _enum;
                    }
                    else if (propertyInfo.PropertyType.IsPrimitive)
                    {
                        MemberType = propertyInfo.PropertyType.ToString();
                    }
                    else if (propertyInfo.PropertyType == typeof(DateTime))
                    {

                        MemberType = _dateTime;
                    }
                    else
                    {
                        MemberType = _object;
                    }
                }

                MemberName = propertyInfo.Name;

                if (propertyInfo.CanRead)
                    Getter = (Func<object, J>)DelegateHelper.GetLambdaPropertyGetter<J>(propertyInfo);

                if (propertyInfo.CanWrite)
                {
                    if (typeof(J) == typeof(DateTime))
                    {
                        Setter = (Action<object, J>)DelegateHelper.GetLambdaPropertySetter<J>(propertyInfo);
                    }
                    else if (typeof(J) == typeof(object))
                    {
                        Setter = (Action<object, object>)DelegateHelper.GetLambdaPropertySetter(propertyInfo);
                    }
                    else
                    {
                        Setter = (Action<object, J>)DelegateHelper.GetLambdaPropertySetter<J>(propertyInfo);
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Creation of delegate on property {propertyInfo.Name}, {BindedInstance?.GetType()} failed. => {e}");
            }

        }

        public void createPropertyDelegatesAuto(PropertyInfo propertyInfo)
        {
            MemberAttributeType = ReflectedMemberDelegateAttributeType.Property;

            if (propertyInfo.PropertyType == typeof(string))
            {
                MemberName = propertyInfo.Name;
                MemberType = _string;
                createPropertyDelegates<T, string>(propertyInfo);
            }
            else if (propertyInfo.PropertyType.IsEnum)
            {
                MemberName = propertyInfo.Name;
                MemberType = _enum;
                createPropertyDelegates<T, int>(propertyInfo);
            }
            else if (propertyInfo.PropertyType.IsPrimitive)
            {
                MemberName = propertyInfo.Name;
                MemberType = propertyInfo.PropertyType.ToString();
                switch (MemberType)
                {
                    case _int32:
                        createPropertyDelegates<T, int>(propertyInfo);
                        break;
                    case _float:
                        createPropertyDelegates<T, float>(propertyInfo);
                        break;
                    case _bool:
                        createPropertyDelegates<T, bool>(propertyInfo);
                        break;
                    case _int16:
                        createPropertyDelegates<T, short>(propertyInfo);
                        break;
                    case _int64:
                        createPropertyDelegates<T, long>(propertyInfo);
                        break;
                    case _byte:
                        createPropertyDelegates<T, byte>(propertyInfo);
                        break;
                    case _double:
                        createPropertyDelegates<T, double>(propertyInfo);
                        break;
                    case _object:
                        createPropertyDelegates<T, object>(propertyInfo);
                        break;

                }
            }
            else if (propertyInfo.PropertyType == typeof(DateTime))
            {

                MemberName = propertyInfo.Name;
                MemberType = _dateTime;
                createPropertyDelegates<T, DateTime>(propertyInfo);
            }
            else
            {
                MemberName = propertyInfo.Name;
                MemberType = _object;
                createPropertyDelegates<T, object>(propertyInfo);
            }

            //UnityEngine.Debug.Log("created delegates for type " + propertyInfo.PropertyType + "   " + propertyInfo.Name + " =>  " + MemberType);
        }

        /// <summary>
        /// Returns the value of the member on the given instance
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public K getValueGeneric<K>(T instance, bool dynamicInvoke = false)
        {
            if (Getter == null)
                return default(K);

            return dynamicInvoke ? (K)((Func<T, K>)Getter).DynamicInvoke(instance) : (K)((Func<T, K>)Getter).Invoke(instance);
        }

        public void setValueGeneric<K>(T instance, K value, bool dynamicInvoke = false)
        {
            if (dynamicInvoke)
                ((Action<T, K>)Setter).DynamicInvoke(instance, value);
            else
                ((Action<T, K>)Setter).Invoke(instance, value);
        }

        public void setBindedValueGeneric<K>(K value)
        {
            setValueGeneric(BindedInstance, value);
        }

        public K getBindedValueGeneric<K>()
        {
            return getValueGeneric<K>(BindedInstance);
        }

        public object getValueDynamic(T instance)
        {
            switch (MemberType)
            {
                case _int16:
                    return getBindedValue<short>(instance);
                case _uint16:
                    return getBindedValue<ushort>(instance);
                case _enum:
                case _int32:
                    return getBindedValue<int>(instance);
                case _uint32:
                    return getBindedValue<uint>(instance);
                case _float:
                    return getBindedValue<float>(instance);
                case _bool:
                    return getBindedValue<bool>(instance);
                case _string:
                    return getBindedValue<string>(instance);
                case _char:
                    return getBindedValue<char>(instance);
                case _int64:
                    return getBindedValue<long>(instance);
                case _uint64:
                    return getBindedValue<ulong>(instance);
                case _byte:
                    return getBindedValue<byte>(instance);
                case _sbyte:
                    return getBindedValue<sbyte>(instance);
                case _double:
                    return getBindedValue<double>(instance);
                case _decimal:
                    return getBindedValue<decimal>(instance);
                case _object:
                    return getBindedValue<object>(instance, true);
                case _dateTime:
                    return getBindedValue<DateTime>(instance);
            }

            throw new Exception("not implemented " + MemberType + " " + MemberName);
        }

        public void setValueDynamic(T instance, object value)
        {
            switch (MemberType)
            {
                case _int16:
                    setValueGeneric<short>(instance, (short)value);
                    break;
                case _uint16:
                    setValueGeneric<ushort>(instance, (ushort)value);
                    break;
                case _enum:
                case _int32:
                    setValueGeneric<int>(instance, (int)value);
                    break;
                case _uint32:
                    setValueGeneric<uint>(instance, (uint)value);
                    break;
                case _float:
                    setValueGeneric<float>(instance, (float)value);
                    break;
                case _bool:
                    setValueGeneric<bool>(instance, (bool)value);
                    break;
                case _string:
                    setValueGeneric<string>(instance, (string)value);
                    break;
                case _int64:
                    setValueGeneric<long>(instance, (long)value);
                    break;
                case _uint64:
                    setValueGeneric<ulong>(instance, (ulong)value);
                    break;
                case _byte:
                    setValueGeneric<byte>(instance, (byte)value);
                    break;
                case _sbyte:
                    setValueGeneric<sbyte>(instance, (sbyte)value);
                    break;
                case _double:
                    setValueGeneric<double>(instance, (double)value);
                    break;
                case _decimal:
                    setValueGeneric<decimal>(instance, (decimal)value);
                    break;
                case _char:
                    setValueGeneric<char>(instance, (char)value);
                    break;
                case _object:
                    setValueGeneric<object>(instance, value);
                    break;
                case _dateTime:
                    setValueGeneric<DateTime>(instance, (DateTime)value);
                    break;
                default:
                    throw new NotImplementedException(value.GetType().ToString());
            }

        }

        /// <summary>
        /// Returns the value of the member on the bindedInstance
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public object getBindedValueDynamic()
        {
            if (BindedInstance == null)
                throw new Exception($"No binded instance avalaible to retrieve get the value of member {MemberType}.{MemberName}");

            return getValueDynamic(BindedInstance);
        }

        public K getBindedValue<K>(T instance, bool dynamicInvoke = false)
        {
            if (MemberAttributeType == ReflectedMemberDelegateAttributeType.Field)
            {
                Func<T, K> genericAction = (Func<T, K>)Getter;
                return dynamicInvoke ? (K)genericAction.DynamicInvoke(instance) : genericAction.Invoke(instance);
            }
            else if (MemberAttributeType == ReflectedMemberDelegateAttributeType.Property)
            {
                Func<T, K> genericAction = (Func<T, K>)Getter;
                return dynamicInvoke ? (K)genericAction.DynamicInvoke(instance) : genericAction.Invoke(instance);
            }
            // Method with no args
            else if (MemberAttributeType == ReflectedMemberDelegateAttributeType.Method)
            {
                Func<T, K> genericAction = (Func<T, K>)Getter;
                return dynamicInvoke ? (K)genericAction.DynamicInvoke(instance) : genericAction.Invoke(instance);
            }
            else
            {
                throw new Exception("Member attribute type error.");
            }
        }

        public void resetValueToDefault(T instance = default(T))
        {
            if (instance == null)
                instance = BindedInstance;

            switch (MemberType)
            {
                case _enum:
                case _int32:
                    setValueGeneric<int>(instance, 0);
                    break;
                case _uint32:
                    setValueGeneric<uint>(instance, 0);
                    break;
                case _float:
                    setValueGeneric<float>(instance, 0);
                    break;
                case _bool:
                    setValueGeneric<bool>(instance, false);
                    break;
                case _string:
                    setValueGeneric<string>(instance, string.Empty);
                    break;
                case _int16:
                    setValueGeneric<short>(instance, 0);
                    break;
                case _uint16:
                    setValueGeneric<ushort>(instance, 0);
                    break;
                case _int64:
                    setValueGeneric<long>(instance, 0);
                    break;
                case _uint64:
                    setValueGeneric<ulong>(instance, 0);
                    break;

                case _byte:
                    setValueGeneric<byte>(instance, 0);
                    break;
                case _double:
                    setValueGeneric<double>(instance, 0);
                    break;
                case _object:
                    setValueGeneric<object>(instance, null, true);
                    break;

            }
        }
    }
}
