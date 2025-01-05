using Codice.Client.Common.TreeGrouper;
using Sirenix.OdinInspector;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
            public float B;
            public Vector3 Vector3;

            public SomeDataStruct(string a, long f, float c, float b, Vector3 vector3)
            {
                A = a;
                F = f;
                C = c;
                B = b;

                Vector3 = vector3;
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
        private void TestMarshalStructSerialize(int runs = 1000)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            var buffer = new byte[0];
            for (int i = 0; i < runs; ++i)
            {
                buffer = StructSerializer.RawSerialize(new SomeDataStruct("A", 16845315752316, 10.5f, 137.3f, Vector3.one) );

            }

            stopwatch.Stop();
            Debug.Log("Serialization : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            stopwatch.Stop();

            for (int i = 0; i < runs; ++i)
            {
                var result = StructSerializer.RawDeserialize<SomeDataStruct>(buffer, 0);

            }
            stopwatch.Stop();
            Debug.Log("Deserialization : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");
        }

        [Button]
        private void IOStruct(int _runs)
        {
            AtomSerializer.Reset();
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            /// 100 000 ~160ms serialize  ~100ms deserialize 
            for (int i = 0; i < _runs; ++i)
            {
                var bytes = AtomSerializer.SerializeGeneric(new SomeDataStruct("A", 16845315752316, 10.5f, 137.3f, Vector3.one));
            }
            stopwatch.Stop();
            Debug.Log("Serialization : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            var _bytes = AtomSerializer.SerializeGeneric(new SomeDataStruct() { A = "A", B = 137.3f, C = 10.5f, F = 14646446545646 });
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < _runs; ++i)
            {
                var data = AtomSerializer.DeserializeGeneric(typeof(SomeDataStruct), _bytes);
            }
            stopwatch.Stop();
            Debug.Log("Deserialization : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");
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

        [Button]
        private void BW_BC_Benchmark(int runs = 1000)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < runs; ++i)
            {
                using (var memoryStream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(memoryStream))
                    {
                        writer.Write(1000);
                        writer.Write("Hello");
                        writer.Write(.38383f);
                        writer.Write(true);
                        var bytes = memoryStream.ToArray();
                    }
                }
            }
            stopwatch.Stop();
            Debug.Log("Serialization with memorystream : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < runs; ++i)
            {
                var bytes = new byte[256];

                var a = BitConverter.GetBytes(1000);
                var b = Encoding.ASCII.GetBytes("Hello");
                var c = BitConverter.GetBytes(.38383f);
                var d = BitConverter.GetBytes(true);

                int writerIndex = 0;
                for (int j = 0; j < a.Length; ++j)
                {
                    bytes[writerIndex++] = a[j];
                    writerIndex++;
                }

                for (int j = 0; j < b.Length; ++j)
                {
                    bytes[writerIndex++] = b[j];
                    writerIndex++;
                }

                for (int j = 0; j < c.Length; ++j)
                {
                    bytes[writerIndex++] = c[j];
                    writerIndex++;
                }

                for (int j = 0; j < d.Length; ++j)
                {
                    bytes[writerIndex++] = d[j];
                    writerIndex++;
                }
            }
            stopwatch.Stop();
            Debug.Log("Serialization with BitConverter : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");
        }

        [Button]
        private void BitConverterBenchmark(int runs = 10000)
        {
            var random = new System.Random();
            int value = random.Next(0, runs);

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            var buffer1 = new byte[4 * runs];
            var writeindex = 0;
            for (int i = 0; i < runs; ++i)
            {
                value = i;
                var runbytes = BitConverter.GetBytes(value);
                //value = random.Next(0, runs);
                for (int j = 0; j < runbytes.Length; ++j)
                {
                    buffer1[writeindex++] = runbytes[j];
                }
            }

            stopwatch.Stop();
            Debug.Log("BitConverter normal : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            var buffer2 = new byte[4 * runs];
            var intbuffer = new byte[4];
            var span = intbuffer.AsSpan();
            writeindex = 0;

            for (int i = 0; i < runs; ++i)
            {
                value = i;
                BitConverter.TryWriteBytes(span, value);
                //value = random.Next(0, runs);
                for (int j = 0; j < span.Length; ++j)
                {
                    buffer2[writeindex++] = span[j];
                }
            }

            stopwatch.Stop();
            Debug.Log("BitConvert span  : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            for (int i = 0; i < buffer1.Length; ++i)
            {
                if (buffer1[i] != buffer2[i])
                    throw new Exception();
            }
        }

    }

    
}

