using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Text;
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
        private void ResetSerializerCache()
        {
            AtomSerializer.Reset();
        }

        [Button]
        private void SimpleValueTypes(int _runs = 10)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            /// 100 000 runs in 281 ms 
            for(int i = 0; i < _runs; ++i)
            {
                var bytes = AtomSerializer.SerializeDynamic(0, "Enzo__854554egr554ge54gre54ger5g4er", 18283842182462864, 3f, 'h', .01515f);
            }
            stopwatch.Stop();
            Debug.Log("Serialization : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            var _bytes = AtomSerializer.SerializeDynamic(0, "Enzo__854554egr554ge54gre54ger5g4er", 18283842182462864, 3f, 'h', .01515f);
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < _runs; ++i)
            {
                var datas = AtomSerializer.DeserializeDynamic(0, _bytes);
            }
            stopwatch.Stop();
            Debug.Log("Deserialization : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            /*foreach (var data in datas)
            {
                Debug.Log(data.ToString());
            }*/
        }

        [Button]
        private void IOSomeParams()
        {
            AtomSerializer.Reset();

            var bytes = AtomSerializer.SerializeDynamic(1, Encoding.ASCII.GetBytes("thisissomebytifiedstring"), new List<int> { 1 });
            var datas = AtomSerializer.DeserializeDynamic(1, bytes);
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
