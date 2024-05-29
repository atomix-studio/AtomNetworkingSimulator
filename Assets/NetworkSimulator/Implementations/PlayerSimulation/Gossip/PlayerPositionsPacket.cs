
using Atom.CommunicationSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Atom.PlayerSimulation
{
    public class PlayerData
    {
        public Vector3 WorldPosition;
        public long PeerID;
        public string PeerAdress;
    }

    /// <summary>
    /// A packet that will be gossiped soon
    /// </summary>
    public class PlayerPositionsPacket : AbstractBroadcastablePacket
    {
        /// <summary>
        /// Contains a list of element that concats a bunch of received datas from previous gossip round + data from local player
        /// </summary>
        public List<PlayerData> Datas;

        public PlayerPositionsPacket(List<PlayerData> datas)
        {
            Datas = datas;
        }

        public PlayerData GetDataForID(long id)
        {
            for (int i = 0; i < Datas.Count; i++)
            {
                if (Datas[i].PeerID == id)
                {
                    return Datas[i];
                }
            }

            return null;
        }

        public bool HasDataForID(long id)
        {
            for (int i = 0; i < Datas.Count; i++)
            {
                if (Datas[i].PeerID == id)
                {
                    return true;
                }
            }

            return false;
        }

        public override INetworkPacket ClonePacket(INetworkPacket received)
        {
            return (PlayerPositionsPacket)(received as PlayerPositionsPacket).MemberwiseClone();
        }
    }
}
