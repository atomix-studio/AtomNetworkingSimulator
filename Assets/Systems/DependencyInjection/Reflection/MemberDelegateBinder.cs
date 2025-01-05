using System;
using System.Reflection;
using Microsoft.CSharp;

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

        public void CreateFieldDelegates<K, J>(FieldInfo fieldInfo)
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

        public void CreateFieldDelegatesAuto(FieldInfo fieldInfo)
        {
            MemberAttributeType = ReflectedMemberDelegateAttributeType.Field;

            if (fieldInfo.FieldType == typeof(string))
            {
                MemberName = fieldInfo.Name;
                MemberType = _string;
                CreateFieldDelegates<T, string>(fieldInfo);
            }
            else if (fieldInfo.FieldType.IsEnum)
            {
                MemberName = fieldInfo.Name;
                MemberType = _enum;
                CreateFieldDelegates<T, int>(fieldInfo);
            }
            else if (fieldInfo.FieldType.IsPrimitive)
            {
                MemberName = fieldInfo.Name;
                MemberType = fieldInfo.FieldType.ToString();
                switch (MemberType)
                {
                    case _uint32:
                        CreateFieldDelegates<T, uint>(fieldInfo);
                        break;
                    case _int32:
                        CreateFieldDelegates<T, int>(fieldInfo);
                        break;
                    case _float:
                        CreateFieldDelegates<T, float>(fieldInfo);
                        break;
                    case _bool:
                        CreateFieldDelegates<T, bool>(fieldInfo);
                        break;
                    case _string:
                        CreateFieldDelegates<T, string>(fieldInfo);
                        break;
                    case _uint16:
                        CreateFieldDelegates<T, ushort>(fieldInfo);
                        break;
                    case _int16:
                        CreateFieldDelegates<T, short>(fieldInfo);
                        break;
                    case _uint64:
                        CreateFieldDelegates<T, ulong>(fieldInfo);
                        break;
                    case _int64:
                        CreateFieldDelegates<T, long>(fieldInfo);
                        break;
                    case _sbyte:
                        CreateFieldDelegates<T, sbyte>(fieldInfo);
                        break;
                    case _byte:
                        CreateFieldDelegates<T, byte>(fieldInfo);
                        break;
                    case _double:
                        CreateFieldDelegates<T, double>(fieldInfo);
                        break;
                    case _object:
                        CreateFieldDelegates<T, object>(fieldInfo);
                        break;
                    default: throw new NotImplementedException(MemberType + " / " + MemberName);
                }
            }
            else if (fieldInfo.FieldType == typeof(DateTime))
            {

                MemberName = fieldInfo.Name;
                MemberType = _dateTime;
                CreateFieldDelegates<T, DateTime>(fieldInfo);
            }
            else
            {
                MemberName = fieldInfo.Name;
                MemberType = _object;
                CreateFieldDelegates<T, object>(fieldInfo);
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

        public void CreatePropertyDelegates<K, J>(PropertyInfo propertyInfo)
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

                // accessing the backing field is way faster than creating a delegate on the getSet adn getget methods
                // we try to look for autogenerated backing fields

                // TODO forcing the backing field name somehow at the call ?
                var backingFieldInfo = propertyInfo.DeclaringType.GetField($"<{propertyInfo.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (backingFieldInfo != null)
                {
                    Getter = DelegateHelper.CreateFieldGetter<K, J>(backingFieldInfo);
                    Setter = DelegateHelper.CreateFieldSetter<K, J>(backingFieldInfo);

                    return;
                    // **********************************************************
                }
                
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

        public void CreatePropertyDelegatesAuto(PropertyInfo propertyInfo)
        {
            MemberAttributeType = ReflectedMemberDelegateAttributeType.Property;

            if (propertyInfo.PropertyType == typeof(string))
            {
                MemberName = propertyInfo.Name;
                MemberType = _string;
                CreatePropertyDelegates<T, string>(propertyInfo);
            }
            else if (propertyInfo.PropertyType.IsEnum)
            {
                MemberName = propertyInfo.Name;
                MemberType = _enum;
                CreatePropertyDelegates<T, int>(propertyInfo);
            }
            else if (propertyInfo.PropertyType.IsPrimitive)
            {
                MemberName = propertyInfo.Name;
                MemberType = propertyInfo.PropertyType.ToString();

                switch (MemberType)
                {
                    case _int16:
                        CreatePropertyDelegates<T, short>(propertyInfo);
                        break;
                    case _uint16:
                        CreatePropertyDelegates<T, ushort>(propertyInfo);
                        break;
                    case _int32:
                        CreatePropertyDelegates<T, int>(propertyInfo);
                        break;
                    case _uint32:
                        CreatePropertyDelegates<T, uint>(propertyInfo);
                        break;
                    case _float:
                        CreatePropertyDelegates<T, float>(propertyInfo);
                        break;
                    case _bool:
                        CreatePropertyDelegates<T, bool>(propertyInfo);
                        break;
                    case _int64:
                        CreatePropertyDelegates<T, long>(propertyInfo);
                        break;
                    case _uint64:
                        CreatePropertyDelegates<T, ulong>(propertyInfo);
                        break;
                    case _byte:
                        CreatePropertyDelegates<T, byte>(propertyInfo);
                        break;
                    case _sbyte:
                        CreatePropertyDelegates<T, sbyte>(propertyInfo);
                        break;
                    case _double:
                        CreatePropertyDelegates<T, double>(propertyInfo);
                        break;
                    case _object:
                        CreatePropertyDelegates<T, object>(propertyInfo);
                        break;
                    default:
                        throw new Exception("Type not implemented " + propertyInfo.PropertyType + "  / " + propertyInfo.Name);
                        break;
                }
            }
            else if (propertyInfo.PropertyType == typeof(DateTime))
            {

                MemberName = propertyInfo.Name;
                MemberType = _dateTime;
                CreatePropertyDelegates<T, DateTime>(propertyInfo);
            }
            else
            {
                MemberName = propertyInfo.Name;
                MemberType = _object;
                CreatePropertyDelegates<T, object>(propertyInfo);
            }

            //UnityEngine.Debug.Log("created delegates for type " + propertyInfo.PropertyType + "   " + propertyInfo.Name + " =>  " + MemberType);
        }

        /// <summary>
        /// Returns the value of the member on the given instance
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public K GetValueGeneric<K>(T instance, bool dynamicInvoke = false)
        {
            if (Getter == null)
                return default(K);

            return dynamicInvoke ? (K)((Func<T, K>)Getter).DynamicInvoke(instance) : (K)((Func<T, K>)Getter).Invoke(instance);
        }

        public void SetValueGeneric<K>(T instance, K value, bool dynamicInvoke = false)
        {
            if (dynamicInvoke)
                ((Action<T, K>)Setter).DynamicInvoke(instance, value);
            else
                ((Action<T, K>)Setter).Invoke(instance, value);
        }

        public void SetBindedValueGeneric<K>(K value)
        {
            SetValueGeneric(BindedInstance, value);
        }

        public K GetBindedValueGeneric<K>()
        {
            return GetValueGeneric<K>(BindedInstance);
        }

        public object GetValueDynamic(T instance)
        {
            switch (MemberType)
            {
                case _int16:
                    return GetBindedValue<short>(instance);
                case _uint16:
                    return GetBindedValue<ushort>(instance);
                case _enum:
                case _int32:
                    return GetBindedValue<int>(instance);
                case _uint32:
                    return GetBindedValue<uint>(instance);
                case _float:
                    return GetBindedValue<float>(instance);
                case _bool:
                    return GetBindedValue<bool>(instance);
                case _string:
                    return GetBindedValue<string>(instance);
                case _char:
                    return GetBindedValue<char>(instance);
                case _int64:
                    return GetBindedValue<long>(instance);
                case _uint64:
                    return GetBindedValue<ulong>(instance);
                case _byte:
                    return GetBindedValue<byte>(instance);
                case _sbyte:
                    return GetBindedValue<sbyte>(instance);
                case _double:
                    return GetBindedValue<double>(instance);
                case _decimal:
                    return GetBindedValue<decimal>(instance);
                case _object:
                    return GetBindedValue<object>(instance, true);
                case _dateTime:
                    return GetBindedValue<DateTime>(instance);
            }

            throw new Exception("not implemented " + MemberType + " " + MemberName);
        }

        public void SetValueDynamic(T instance, object value)
        {
            switch (MemberType)
            {
                case _int16:
                    SetValueGeneric<short>(instance, (short)value);
                    break;
                case _uint16:
                    SetValueGeneric<ushort>(instance, (ushort)value);
                    break;
                case _enum:
                case _int32:
                    SetValueGeneric<int>(instance, (int)value);
                    break;
                case _uint32:
                    SetValueGeneric<uint>(instance, (uint)value);
                    break;
                case _float:
                    SetValueGeneric<float>(instance, (float)value);
                    break;
                case _bool:
                    SetValueGeneric<bool>(instance, (bool)value);
                    break;
                case _string:
                    SetValueGeneric<string>(instance, (string)value);
                    break;
                case _int64:
                    SetValueGeneric<long>(instance, (long)value);
                    break;
                case _uint64:
                    SetValueGeneric<ulong>(instance, (ulong)value);
                    break;
                case _byte:
                    SetValueGeneric<byte>(instance, (byte)value);
                    break;
                case _sbyte:
                    SetValueGeneric<sbyte>(instance, (sbyte)value);
                    break;
                case _double:
                    SetValueGeneric<double>(instance, (double)value);
                    break;
                case _decimal:
                    SetValueGeneric<decimal>(instance, (decimal)value);
                    break;
                case _char:
                    SetValueGeneric<char>(instance, (char)value);
                    break;
                case _object:
                    SetValueGeneric<object>(instance, value);
                    break;
                case _dateTime:
                    SetValueGeneric<DateTime>(instance, (DateTime)value);
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
        public object GetBindedValueDynamic()
        {
            if (BindedInstance == null)
                throw new Exception($"No binded instance avalaible to retrieve get the value of member {MemberType}.{MemberName}");

            return GetValueDynamic(BindedInstance);
        }

        public K GetBindedValue<K>(T instance, bool dynamicInvoke = false)
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

        public void ResetValueToDefault(T instance = default(T))
        {
            if (instance == null)
                instance = BindedInstance;

            switch (MemberType)
            {
                case _enum:
                case _int32:
                    SetValueGeneric<int>(instance, 0);
                    break;
                case _uint32:
                    SetValueGeneric<uint>(instance, 0);
                    break;
                case _float:
                    SetValueGeneric<float>(instance, 0);
                    break;
                case _bool:
                    SetValueGeneric<bool>(instance, false);
                    break;
                case _string:
                    SetValueGeneric<string>(instance, string.Empty);
                    break;
                case _int16:
                    SetValueGeneric<short>(instance, 0);
                    break;
                case _uint16:
                    SetValueGeneric<ushort>(instance, 0);
                    break;
                case _int64:
                    SetValueGeneric<long>(instance, 0);
                    break;
                case _uint64:
                    SetValueGeneric<ulong>(instance, 0);
                    break;

                case _byte:
                    SetValueGeneric<byte>(instance, 0);
                    break;
                case _double:
                    SetValueGeneric<double>(instance, 0);
                    break;
                case _object:
                    SetValueGeneric<object>(instance, null, true);
                    break;

            }
        }
    }
}
