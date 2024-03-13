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

            var fields = argType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; ++i)
            {
                _serializationDatas.Add(new MemberSerializationData(fields[i].FieldType));

                _memberDelegateBinders.Add(new DynamicMemberDelegateBinder());
                _memberDelegateBinders[i].createFieldDelegatesAuto(fields[i]);

                byte_length += _serializationDatas[i].fixedByteLength;

                if (_serializationDatas[i].isDynamicSize)
                    isDynamicSize = true;
            }

            var properties = argType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < properties.Length; ++i)
            {
                _serializationDatas.Add(new MemberSerializationData(properties[i].PropertyType));

                _memberDelegateBinders.Add(new DynamicMemberDelegateBinder());
                _memberDelegateBinders[i].createPropertyDelegatesAuto(properties[i]);

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

            // faire tourner les binders pour récup les valeurs sur l'object
            for (int i = 0; i < _memberDelegateBinders.Count; ++i)
            {
                var arg = _memberDelegateBinders[i].getValueDynamic(instance);
                _serializationDatas[i].Write(arg, ref _writebuffer, ref writeIndex);
            }

            return _writebuffer;
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
                    switch (_serializationDatas[i].AtomMemberType)
                    {
                        case AtomMemberTypes.Byte:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (byte)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.SByte:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (sbyte)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Short:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (short)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.UShort:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (ushort)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Int:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (int)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.UInt:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (uint)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Long:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (long)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.ULong:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (ulong)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Float:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (float)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Double:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (double)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Bool:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (bool)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Char:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (char)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.String:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (string)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Decimal:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (decimal)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Enum:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, (int)_readbuffer[i]);
                            break;
                        case AtomMemberTypes.Object:
                            _memberDelegateBinders[i].setValueGeneric(new_instance, _readbuffer[i]);
                            break;
                    }
                }
            }

            return new_instance;
        }
    }
}
