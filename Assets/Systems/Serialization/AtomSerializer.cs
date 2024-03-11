using System;
using System.Collections.Generic;

namespace Atom.Serialization
{

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
        public static byte[] SerializeDynamic(ushort payloadIdentifier, params object[] arguments)
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
