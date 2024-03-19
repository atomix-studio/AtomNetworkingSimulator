using Atom.Components.GraphNetwork;
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
        public DateTime B;            
        public short C;  
        public uint D;            
        public long E;          
        public int F;            
        public long G;            
        public long H;
        public long I;

        public TestClass() { }
        public TestClass(string a, DateTime b, short c, uint d, long e, int f, long g, long h)
        {
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
            F = f;
            G = g;
            H = h;
        }

        public short packetTypeIdentifier;
        public long packetUniqueId;
        public long senderID;
        public DateTime sentTime;

        public void DisposePacket()
        {
            throw new NotImplementedException();
        }
    }

    public class PacketSerializationTest : MonoBehaviour
    {
        [Button]
        private void TestSerialize(int runs = 10000)
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

            for (int i =  0; i < runs; i++)
            {
                var bytes = ((INetworkPacket)somePacket).SerializePacket();
                var packet = (TakeLeadRequestPacket)((INetworkPacket)somePacket).DeserializePacket(bytes);
/*                Debug.Log(bytes.Length);
                Debug.Log(packet.prospectId + " " + packet.prospectAdress);
*/
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
        private void TestSerializeTestClass(int runs = 10000)
        {
            // 10000 runs 198-220 ms
            var teest = new TestClass("lol", DateTime.Now, 133, 44432, 13933992223, 5111, 18727377372727, 188181818);

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            var type = teest.GetType();
            for (int i = 0; i < runs; i++)
            {
                var bytes = AtomSerializer.SerializeGeneric(teest);
                var packet = AtomSerializer.DeserializeGeneric(type, bytes);

                /*                Debug.Log(bytes.Length);
                                Debug.Log(packet.prospectId + " " + packet.prospectAdress);
                */
            }
            stopwatch.Stop();
            Debug.Log("S/D : " + stopwatch.ElapsedTicks + "ticks / " + stopwatch.ElapsedMilliseconds + "ms");

        }
    }
}
