using Atom.DependencyProvider;
using System;
using System.Collections.Generic;

namespace Atom.Serialization
{
    public class GenericAtomSerializer
    {
        private List<MemberSerializationData> _serializationDatas { get; set; }
        private List<DynamicMemberDelegateBinder> _memberDelegateBinders { get; set; }

        public int byteLenght { get; private set; }

        // true if one of the MemberSerializationData has a dynamic size aka a serialized byte size that's only known at runtime (if it represent an array and collections, strings)
        public bool isDynamicSize { get; private set; }

        private byte[] _writebuffer;
        private object[] _readbuffer;
        private Type _reflectedType;

        public GenericAtomSerializer(Type argType, int maxWriteBufferSize = 1024)
        {
            _reflectedType = argType;
            _serializationDatas = new List<MemberSerializationData>();
            _memberDelegateBinders = new List<DynamicMemberDelegateBinder>();

            int byte_length = 0;

            var fields = argType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < fields.Length; ++i)
            {
                _serializationDatas.Add(new MemberSerializationData(fields[i].FieldType));

                _memberDelegateBinders.Add(new DynamicMemberDelegateBinder());
                _memberDelegateBinders[i].CreateFieldDelegatesAuto(fields[i]);

                byte_length += _serializationDatas[i].fixedByteLength;

                if (_serializationDatas[i].isDynamicSize)
                    isDynamicSize = true;
            }

            var properties = argType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < properties.Length; ++i)
            {
                _serializationDatas.Add(new MemberSerializationData(properties[i].PropertyType));

                _memberDelegateBinders.Add(new DynamicMemberDelegateBinder());
                _memberDelegateBinders[i].CreatePropertyDelegatesAuto(properties[i]);
                
                if (_serializationDatas[i].isDynamicSize)
                    isDynamicSize = true;

                byte_length += _serializationDatas[i].fixedByteLength;
            }

            if (!isDynamicSize)
            {
                _writebuffer = new byte[byte_length];
            }
            else
            {
                _writebuffer = new byte[maxWriteBufferSize];
            }
            //_writebuffer = new byte[byteLenght];
            _readbuffer = new object[_serializationDatas.Count];
        }

        public byte[] Serialize(object instance)
        {
            int writeIndex = 0;

            // TODO USE SPAN

            // faire tourner les binders pour récup les valeurs sur l'object
            for (int i = 0; i < _memberDelegateBinders.Count; ++i)
            {
                var arg = _memberDelegateBinders[i].GetValueDynamic(instance);

                //UnityEngine.Debug.Log("write data for member " + _memberDelegateBinders[i].MemberName + " " + _memberDelegateBinders[i].MemberType);
                _serializationDatas[i].Write(arg, ref _writebuffer, ref writeIndex);
            }

            var result = new byte[writeIndex];
            Buffer.BlockCopy(_writebuffer, 0, result, 0, writeIndex);
            return result;
        }

        public object Deserialize(byte[] data)
        {
            int readIndex = 0;
            var new_instance = Activator.CreateInstance(_reflectedType);
            for (int i = 0; i < _serializationDatas.Count; ++i)
            {
                _readbuffer[i] = _serializationDatas[i].Read(ref data, ref readIndex);

                if (_serializationDatas[i].IsCollection)
                {
                    switch (_serializationDatas[i].AtomMemberType)
                    {
                        case AtomMemberTypes.Byte:
                            break;
                        case AtomMemberTypes.SByte:
                            break;
                        case AtomMemberTypes.Short:
                            break;
                        case AtomMemberTypes.UShort:
                            break;
                        case AtomMemberTypes.Int:
                            break;
                        case AtomMemberTypes.UInt:
                            break;
                        case AtomMemberTypes.Long:
                            break;
                        case AtomMemberTypes.ULong:
                            break;
                        case AtomMemberTypes.Float:
                            break;
                        case AtomMemberTypes.Double:
                            break;
                        case AtomMemberTypes.Bool:
                            break;
                        case AtomMemberTypes.Char:
                            break;
                        case AtomMemberTypes.String:
                            break;
                        case AtomMemberTypes.Decimal:
                            break;
                        case AtomMemberTypes.Enum:
                            break;
                        case AtomMemberTypes.Object:
                            break;
                    }
                }
                else
                {
                    _memberDelegateBinders[i].SetValueDynamic(new_instance, _readbuffer[i]);
                }
            }

            return new_instance;
        }
    }
}
