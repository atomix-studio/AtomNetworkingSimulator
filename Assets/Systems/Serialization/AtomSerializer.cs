using System;
using System.Collections.Generic;

namespace Atom.Serialization
{
    public enum AtomMemberTypes
    {
        Byte, // 1
        SByte, // 1
        Short, // 2
        UShort, // 2
        Int, // 4
        UInt, // 4
        Long, // 8
        ULong, // 8
        Float, // 4
        Double, // 8
        Bool, // 1
        Char, // 2
        String, // 4b ++
        Decimal, // 24
        DateTime, // 8
        DateSpan,
        Enum, // 4 ?
        Object, // dyn
    }

    /// <summary>
    /// Represent a field/property of a serialized type
    /// If the AtomMemberTypes is Object, then the RecursiveBinder won't be null and will be a collection of primitive types or other recursive 
    /// </summary>
    public class MemberSerializationData
    {
        private const string _int16 = "System.Int16";
        private const string _int32 = "System.Int32";
        private const string _int64 = "System.Int64";
        private const string _byte = "System.Byte";
        private const string _float = "System.Single";
        private const string _double = "System.Double";
        private const string _bool = "System.Boolean";
        private const string _string = "System.String";
        private const string _enum = "System.Enum";
        private const string _object = "System.Object";

        public AtomMemberTypes AtomMemberType { get; set; }

        /// <summary>
        /// for non primitive types, a serialization binder is recursively created
        /// </summary>
        public GenericAtomSerializer GenericRecursiveBinder { get; set; } = null;
        public DynamicAtomSerializer DynamicRecursiveBinder { get; set; } = null;

        public int byteLenght { get; private set; }

        public MemberSerializationData(object arg)
        {
            AtomMemberType = setMemberType(arg.GetType());

            if (AtomMemberType == AtomMemberTypes.Object)
            {
                // recursive serializer doesn't need identifiers as they are 'anonymous' in the sense
                // that we won't ever need to access to it directly from the SerializerClass
                DynamicRecursiveBinder = new DynamicAtomSerializer(0, new object[] { arg });
                byteLenght = DynamicRecursiveBinder.byteLenght;
            }
            else
                byteLenght = getTypeLength(arg);
        }

        public dynamic FromByte(ref byte[] _buffer, ref int readIndex)
        {
            return null;
        }

        public byte[] FromObject(object obj, ref byte[] _buffer, ref int writeIndex)
        {
            return null;
        }

        public AtomMemberTypes setMemberType(Type type)
        {
            // to do finishing all primitive types

            string tString = type.FullName;
            switch (tString)
            {
                case _enum: return AtomMemberTypes.Enum;
                case _int32: return AtomMemberTypes.Int;
                case _float: return AtomMemberTypes.Float;
                case _bool: return AtomMemberTypes.Bool;
                case _string: return AtomMemberTypes.String;
                case _int16: return AtomMemberTypes.Short;
                case _int64: return AtomMemberTypes.Long;
                case _byte: return AtomMemberTypes.Byte;
                case _double: return AtomMemberTypes.Double;
                case _object: return AtomMemberTypes.Object;
            }

            throw new Exception($"Type not implemented exception {type}");
        }

        public int getTypeLength(object obj)
        {
            switch (AtomMemberType)
            {
                case AtomMemberTypes.Byte: return 1;
                case AtomMemberTypes.SByte: return 1;
                case AtomMemberTypes.Short: return 2;
                case AtomMemberTypes.UShort: return 2;
                case AtomMemberTypes.Int: return 4;
                case AtomMemberTypes.UInt: return 4;
                case AtomMemberTypes.Long: return 8;
                case AtomMemberTypes.ULong: return 8;
                case AtomMemberTypes.Float: return 4;
                case AtomMemberTypes.Double: return 8;
                case AtomMemberTypes.Bool: return 1;
                case AtomMemberTypes.Char: return 2;
                case AtomMemberTypes.String: return System.Text.ASCIIEncoding.Unicode.GetByteCount((string)obj);
                case AtomMemberTypes.Decimal: return 24;
                case AtomMemberTypes.DateTime: return 8;
                case AtomMemberTypes.DateSpan: return 16;
                case AtomMemberTypes.Enum: return 4;
            }

            return 0;
        }
    }

    public class GenericAtomSerializer
    {
        public Type SerializedType;
        public List<MemberSerializationData> SerializationDatas { get; set; }
    }

    public class DynamicAtomSerializer
    {
        public ushort SerializedIdentifier;
        public List<MemberSerializationData> SerializationDatas { get; set; }

        public int byteLenght { get; private set; }

        // initialized once
        private byte[] _writebuffer;
        private object[] _readbuffer;

        public DynamicAtomSerializer(ushort serializedIdentifier, object[] args)
        {
            SerializedIdentifier = serializedIdentifier;
            SerializationDatas = new List<MemberSerializationData>();

            int length = 0;
            for (int i = 0; i < args.Length; ++i)
            {
                var md = new MemberSerializationData(args[i]);
                length += md.byteLenght;
                SerializationDatas.Add(md);
            }

            byteLenght = length;
            _writebuffer = new byte[byteLenght];
            _readbuffer = new object[args.Length];
        }

        public byte[] Serialize(object[] args)
        {
            int writeIndex = 0;

            for (int i = 0; i < SerializationDatas.Count; ++i)
            {
                SerializationDatas[i].FromObject(args[i], ref _writebuffer, ref writeIndex);
            }

            return _writebuffer;
        }

        public object[] Deserialize(byte[] data)
        {
            int readIndex = 0;
            for (int i = 0; i < SerializationDatas.Count; ++i)
            {
                _readbuffer[i] = SerializationDatas[i].FromByte(ref data, ref readIndex);
            }
            return _readbuffer;
        }
    }

    public static class AtomSerializer
    {
        private const int _serializationDepth = 3;

        private static Dictionary<Type, GenericAtomSerializer> _genericSerializes = new Dictionary<Type, GenericAtomSerializer>();
        private static Dictionary<ushort, DynamicAtomSerializer> _dynamicSerializers = new Dictionary<ushort, DynamicAtomSerializer>();

        // todo generation of binders for serializable classes/structs that can be used as is to deserialize a new instance or serialize to byte[] 

        #region Generic



        #endregion

        #region Dynamic 

        /// <summary>
        /// Payload identifier allows the serializer to create a custom delegate collection for all the objects array types.
        /// It will be able to deserialize it later 
        /// </summary>
        /// <param name="payloadIdentifier"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static byte[] SerializeDynamic(ushort payloadIdentifier, object[] arguments)
        {
            if (_dynamicSerializers.TryGetValue(payloadIdentifier, out var serializer))
                return serializer.Serialize(arguments);

            _dynamicSerializers.Add(payloadIdentifier, new DynamicAtomSerializer(payloadIdentifier, arguments));
            return _dynamicSerializers[payloadIdentifier].Serialize(arguments);
        }

        public static object[] DeserializeDynamic(ushort payloadIdentifier, byte[] data)
        {
            if (_dynamicSerializers.TryGetValue(payloadIdentifier, out var serializer))
                return serializer.Deserialize(data);

            throw new Exception($"Serializer with identifier {payloadIdentifier} hasn't been generated yet. The serialize() should be called before the deserialize() at least once.");
        }

        #endregion


        #region internal serialize types



        #endregion
    }
}
