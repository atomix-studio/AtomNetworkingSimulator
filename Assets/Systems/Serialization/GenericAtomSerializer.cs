using System;
using System.Collections.Generic;

namespace Atom.Serialization
{
    public class GenericAtomSerializer
    {
        public Type SerializedType;
        public List<MemberSerializationData> SerializationDatas { get; set; }
        public int byteLenght { get; private set; }

        public byte[] Serialize(object obj)
        {
            return null;
        }

        public object Deserialize(byte[] datas)
        {
            return null;
        }
    }
}
