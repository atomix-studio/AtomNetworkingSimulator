using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Serialization.Testing
{
    internal class AtomSerializerTesting : MonoBehaviour
    {
        public struct SomeDataStruct
        {
            public string Name;
            public long Value;
            public byte[] Data;

            public SomeDataStruct(string name, long value, byte[] data)
            {
                Name = name;
                Value = value;
                Data = data;
            }
        }

        public class SomeDataClass
        {

        }

        [Button]
        private void IOSomeParams()
        {
            AtomSerializer.Reset();

            var bytes = AtomSerializer.SerializeDynamic(0, "Enzo", 18283842182462864, new List<int> { 1 }, Encoding.ASCII.GetBytes("thisissomebytifiedstring"));
            var datas = AtomSerializer.DeserializeDynamic(0, bytes);
            for(int i = 0; i < datas.Length; i++)
            {
                Debug.Log(datas[i].ToString());
            }
        }

        private void SerializeSomeDataStruct()
        {
            var str = new SomeDataStruct("Enzo", 18283842182462864, Encoding.ASCII.GetBytes("thisissomebytifiedstring"));
        }
    }
}
