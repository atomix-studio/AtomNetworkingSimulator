using System.Collections.Generic;

namespace Atom.Serialization
{
    public class DynamicAtomSerializer
    {
        public ushort SerializedIdentifier;
        public List<MemberSerializationData> SerializationDatas { get; set; }

        public int byteLenght { get; private set; }

        private byte[] _writebuffer = new byte[2048];
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
            //_writebuffer = new byte[byteLenght];
            _readbuffer = new object[args.Length];
        }

        public byte[] Serialize(object[] args)
        {
            int writeIndex = 0;

            for (int i = 0; i < SerializationDatas.Count; ++i)
            {
                SerializationDatas[i].Write(args[i], ref _writebuffer, ref writeIndex);
            }

            return _writebuffer;
        }

        public object[] Deserialize(byte[] data)
        {
            int readIndex = 0;
            for (int i = 0; i < SerializationDatas.Count; ++i)
            {
                _readbuffer[i] = SerializationDatas[i].Read(ref data, ref readIndex);
            }
            return _readbuffer;
        }
    }
}
