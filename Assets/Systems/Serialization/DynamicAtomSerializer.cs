using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Atom.Serialization
{
    public class DynamicAtomSerializer
    {
        public ushort SerializedIdentifier;
        public List<MemberSerializationData> SerializationDatas { get; set; }

        public int writtenBytesLenght { get; private set; }
        public int readObjectsLength { get; private set; }

        private byte[] _writebuffer;
        private object[] _readbuffer;

        public DynamicAtomSerializer(ushort serializedIdentifier, object[] args, int maxWriteBufferSize = 1024)
        {
            SerializedIdentifier = serializedIdentifier;
            SerializationDatas = new List<MemberSerializationData>();

            int byte_length = 0;
            bool is_dyn_size = false;
            for (int i = 0; i < args.Length; ++i)
            {
                var md = new MemberSerializationData(args[i].GetType());
                byte_length += md.fixedByteLength;
                if(md.isDynamicSize)
                    is_dyn_size = true;

                SerializationDatas.Add(md);
            }

            writtenBytesLenght = byte_length;
            if(!is_dyn_size)
            {
                _writebuffer = new byte[writtenBytesLenght];
            }
            else
            {
                _writebuffer = new byte[maxWriteBufferSize];
            }
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
