using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Atom.Serialization
{

    public static class AtomSerializer
    {
        private const int _serializationDepth = 3;

        private static ConcurrentDictionary<Type, GenericAtomSerializer> _genericSerializers = new ConcurrentDictionary<Type, GenericAtomSerializer>();
        private static ConcurrentDictionary<ushort, DynamicAtomSerializer> _dynamicSerializers = new ConcurrentDictionary<ushort, DynamicAtomSerializer>();

        // todo generation of binders for serializable classes/structs that can be used as is to deserialize a new instance or serialize to byte[] 

        public static void Reset()
        {
            _genericSerializers.Clear();
            _dynamicSerializers.Clear();
        }

        #region Generic

        public static byte[] SerializeGeneric(object instance)
        {
            var type = instance.GetType();
            if (_genericSerializers.TryGetValue(type, out var serializer))
                return serializer.Serialize(instance);

            _genericSerializers.TryAdd(type, new GenericAtomSerializer(type));
            return _genericSerializers[type].Serialize(instance);
        }

        public static object DeserializeGeneric(Type type, byte[] data)
        {
            if (_genericSerializers.TryGetValue(type, out var serializer))
                return serializer.Deserialize(data);

            throw new Exception($"Serializer with identifier {type} hasn't been generated yet. The serialize() should be called before the deserialize() at least once.");
        }
        #endregion

        #region Dynamic 

        /// <summary>
        /// Payload identifier allows the serializer to create a custom delegate collection for all the objects array types.
        /// It will be able to deserialize it later 
        /// </summary>
        /// <param name="payloadIdentifier"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static byte[] SerializeDynamic(ushort payloadIdentifier, params object[] arguments)
        {
            if (_dynamicSerializers.TryGetValue(payloadIdentifier, out var serializer))
                return serializer.Serialize(arguments);

            _dynamicSerializers.TryAdd(payloadIdentifier, new DynamicAtomSerializer(payloadIdentifier, arguments));
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
