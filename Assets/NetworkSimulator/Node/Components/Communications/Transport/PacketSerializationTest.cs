using Atom.Components.GraphNetwork;
using Atom.DependencyProvider;
using Atom.Serialization;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace Atom.CommunicationSystem
{
    public class TestClass
    {
        public string A;
        public bool B;
        public short C;
        public uint D;
        public int[] E = new int[] { 1, 2, 3, 4, 5, 6 };

        public TestClass() { }
        public TestClass(string a, bool b, short c, uint d)
        {
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public void DisposePacket()
        {
            throw new NotImplementedException();
        }
    }

    public class TestClassProps
    {
        public string A { get; set; }
        public bool B { get; set; }
        public short C { get; set; }
        public uint D { get; set; }
        public int[] E { get; set; } = new int[] { 1, 2, 3, 4, 5, 6 };

        public TestClassProps() { }
        public TestClassProps(string a, bool b, short c, uint d)
        {
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public void DisposePacket()
        {
            throw new NotImplementedException();
        }
    }

    public class PacketSerializationTest : MonoBehaviour
    {
        [Button]
        private void TestPacketSerializationDeserialization(int runs = 10000)
        {
            // 10000 runs 198-220 ms
            var somePacket = new TakeLeadRequestPacket(100, "somepacketIP");
            somePacket.packetUniqueId = 10100033000000;
            somePacket.sentTime = DateTime.UtcNow;
            somePacket.packetTypeIdentifier = 12;
            somePacket.senderID = 5050;
            somePacket.broadcasterID = 2020;
            somePacket.broadcastID = 42;
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < runs; i++)
            {
                var bytes = ((INetworkPacket)somePacket).SerializePacket();
                var packet = (TakeLeadRequestPacket)((INetworkPacket)somePacket).DeserializePacket(bytes);

            }
            stopwatch.Stop();
            Debug.Log("S/D : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");


            // 10000 runs 198-220 ms
            somePacket = new TakeLeadRequestPacket(100, "somepacketIP");
            somePacket.packetUniqueId = 10100033000000;
            somePacket.sentTime = DateTime.UtcNow;
            somePacket.packetTypeIdentifier = 12;
            somePacket.senderID = 5050;
            somePacket.broadcasterID = 2020;
            somePacket.broadcastID = 42;
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            var type = somePacket.GetType();
            for (int i = 0; i < runs; i++)
            {
                var bytes = AtomSerializer.SerializeGeneric(somePacket);
                var packet = AtomSerializer.DeserializeGeneric(type, bytes);
            }
            stopwatch.Stop();
            Debug.Log("S/D 2 : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

        }

        [Button]
        private void SerializeFieldVsPropertyClasses(int runs = 10000)
        {
            // 10000 runs 198-220 ms
            var teest = new TestClass("lol", true, 133, 44432);
            var random = new System.Random();
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            var type = teest.GetType();
            for (int i = 0; i < runs; i++)
            {

                var bytes = AtomSerializer.SerializeGeneric(teest);
                var packet = AtomSerializer.DeserializeGeneric(type, bytes);
                teest = new TestClass(System.Guid.NewGuid().ToString(), true, (short)DateTime.Now.Ticks, (uint)random.Next());

                /*                Debug.Log(bytes.Length);
                                Debug.Log(packet.prospectId + " " + packet.prospectAdress);
                */
            }
            stopwatch.Stop();
            Debug.Log("S/D FIELD : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            var teest2 = new TestClassProps("lol", true, 133, 44432);

            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            type = teest2.GetType();

            for (int i = 0; i < runs; i++)
            {
                var bytes = AtomSerializer.SerializeGeneric(teest2);
                var packet = AtomSerializer.DeserializeGeneric(type, bytes);
                teest2 = new TestClassProps(System.Guid.NewGuid().ToString(), true, (short)DateTime.Now.Ticks, (uint)random.Next());
            }
            stopwatch.Stop();
            Debug.Log("S/D PROPS : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

        }

        public int SomeField;
        public int SomeAutoProperty { get; set; }
        [Button]
        private void TestBackingField(int runs = 10000)
        {
            var field_binder = new MemberDelegateBinder<PacketSerializationTest>();
            field_binder.CreateFieldDelegatesAuto(this.GetType().GetField(nameof(SomeField)));

            var prop_binder = new MemberDelegateBinder<PacketSerializationTest>();
            prop_binder.CreatePropertyDelegatesAuto(this.GetType().GetProperty(nameof(SomeAutoProperty)));
            var random = new System.Random(500);

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < runs; ++i)
            {
                field_binder.SetValueDynamic(this, random.Next());
                int rnd = (int)field_binder.GetValueDynamic(this);
            }
            stopwatch.Stop();
            Debug.Log("S/D Field : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            random = new System.Random(500);
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < runs; ++i)
            {
                prop_binder.SetValueDynamic(this, random.Next());
                int rnd = (int)prop_binder.GetValueDynamic(this);
            }
            Debug.Log("S/D AutoProp: " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

        }

        [Button]
        private void TestClassInstantiate(int runs = 10000)
        {
            var random = new System.Random(500);

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < runs; ++i)
            {
                //new TestClass(System.Guid.NewGuid().ToString(), true, (short)DateTime.Now.Ticks, (uint)random.Next());
                Activator.CreateInstance(typeof(TestClass));
            }
            stopwatch.Stop();
            Debug.Log("S/D Field : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

            random = new System.Random(500);
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < runs; ++i)
            {
                //new TestClassProps(System.Guid.NewGuid().ToString(), true, (short)DateTime.Now.Ticks, (uint)random.Next());

                Activator.CreateInstance(typeof(TestClassProps));
            }
            Debug.Log("S/D AutoProp: " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

        }

        [Button]
        private void IOParallel(int runs = 10000)
        {
            // 10000 runs 198-220 ms
            var random = new System.Random(1222);
            var stopwatch = new System.Diagnostics.Stopwatch();
            var type = typeof(TestClass);
            stopwatch.Start();

            Parallel.For(0, runs, (index) =>
            {
                var teest = new TestClass(System.Guid.NewGuid().ToString(), true, (short)DateTime.Now.Ticks, (uint)random.Next());
                var bytes = AtomSerializer.SerializeGeneric(teest);
                var packet = AtomSerializer.DeserializeGeneric(type, bytes);

            });
         
            stopwatch.Stop();
            Debug.Log("S/D FIELD : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");


            type = typeof(TestClassProps);
            random = new System.Random(1222);
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            Parallel.For(0, runs, (index) =>
            {
                var teest2 = new TestClassProps(System.Guid.NewGuid().ToString(), true, (short)DateTime.Now.Ticks, (uint)random.Next());
                var bytes = AtomSerializer.SerializeGeneric(teest2);
                var packet = AtomSerializer.DeserializeGeneric(type, bytes);

            });
        
            stopwatch.Stop();
            Debug.Log("S/D PROPS : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

        }
    }
}
