using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Atom.Serialization
{
    public class DynamicAtomSerializer
    {
        public ushort SerializedIdentifier;
        public List<MemberSerializationData> SerializationDatas { get; set; }

        /// <summary>
        /// fixed size buffering is fastest, suitable for mesasges without string or collections/enumerables
        /// </summary>
        public bool fixedLength { get; private set; }

        public int writtenBytesLenght { get; private set; }
        public int readObjectsLength { get; private set; }

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
                length += md.fixedByteLength;
                SerializationDatas.Add(md);
            }

            writtenBytesLenght = length;
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
