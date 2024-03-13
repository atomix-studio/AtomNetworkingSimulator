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
            public string A;
            public long F;
            public float C;
            public char E;
            public float B;

            public SomeDataStruct(string a, long f, float c, char e, float b)
            {
                A = a;
                F = f;
                C = c;
                E = e;
                B = b;
            }
        }

        public class SomeDataClass
        {
            public string A;
            public long F;
            public float C;
            public char E;
            public float B;

            public SomeDataClass() { }

            public SomeDataClass(string a, long f, float c, char e, float b)
            {
                A = a;
                F = f;
                C = c;
                E = e;
                B = b;
            }
        }


        [Button]
        private void ResetSerializerCache()
        {
            AtomSerializer.Reset();
        }

        [Button]
        private void IOParams(int _runs = 10)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            /// 100 000 ~190ms serialize  ~100ms deserialize 
            for (int i = 0; i < _runs; ++i)
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
        private void IOClass(int _runs)
        {
            AtomSerializer.Reset();
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            /// 100 000 ~160ms serialize  ~100ms deserialize 
            for (int i = 0; i < _runs; ++i)
            {
                var bytes = AtomSerializer.SerializeGeneric(new SomeDataClass("Enzo__854554egr554ge54gre54ger5g4er", 18283842182462864, 3f, 'h', .01515f));
            }
            stopwatch.Stop();
            Debug.Log("Serialization : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            var _bytes = AtomSerializer.SerializeGeneric(new SomeDataClass("Enzo__854554egr554ge54gre54ger5g4er", 18283842182462864, 3f, 'h', .01515f));
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < _runs; ++i)
            {
                var data = AtomSerializer.DeserializeGeneric(typeof(SomeDataClass), _bytes);
            }
            stopwatch.Stop();
            Debug.Log("Deserialization : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");
        }

        [Button]
        private void IOStruct()
        {
            var str = new SomeDataStruct("Enzo__854554egr554ge54gre54ger5g4er", 18283842182462864, 3f, 'h', .01515f);
        }

        [Button]
        private void IOParamsWithCollections()
        {
            AtomSerializer.Reset();

            var bytes = AtomSerializer.SerializeDynamic(1, Encoding.ASCII.GetBytes("thisissomebytifiedstring"), new List<int> { 1 });
            var datas = AtomSerializer.DeserializeDynamic(1, bytes);
            for (int i = 0; i < datas.Length; i++)
            {
                Debug.Log(datas[i].ToString());
            }
        }

    }
}
