
using Atom.CommunicationSystem;
using Atom.Components.Gossip;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atom.PlayerSimulation
{
    public class PlayerData
    {
        /// <summary>
        /// Position of the player
        /// </summary>
        public Vector3 WorldPosition
        {
            get
            {
                return new Vector3(WorldPositionX, WorldPositionY, WorldPositionZ);
            }
            set
            {
                WorldPositionX = value.x;
                WorldPositionY = value.y;
                WorldPositionZ = value.z;
            }
        }

        public float WorldPositionX;
        public float WorldPositionY;
        public float WorldPositionZ;

        /// <summary>
        /// Addree/ID of the player
        /// </summary>
        public long PeerID;
        public string PeerAdress;

        /// <summary>
        /// Can be used to tell to other that a player is disconnected, if the local peer received data about it
        /// </summary>
        public bool IsAlive;
    }

    /// <summary>
    /// A packet that will be gossiped soon
    /// </summary>
    public class PlayerInfoPacket : AbstractBroadcastablePacket, IGossipPacket
    {
        /// <summary>
        /// Contains a list of element that concats a bunch of received datas from previous gossip round + data from local player
        /// </summary>
        public List<PlayerData> Datas;

        public DateTime gossipStartedTime { get; set; }
        public long gossipId { get; set; }
        public int gossipGeneration { get; set; }

        public PlayerInfoPacket(List<PlayerData> datas)
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
            return (PlayerInfoPacket)(received as PlayerInfoPacket).MemberwiseClone();
        }
    }
}
