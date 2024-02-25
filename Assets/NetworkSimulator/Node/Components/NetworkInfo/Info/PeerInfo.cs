using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.CommunicationSystem
{
    [Serializable]
    /// <summary>
    /// Holds data of a peer (adress, lease, etc)
    /// </summary>
    public class PeerInfo
    {
        public PeerInfo() { }

        public PeerInfo(string senderID, string peerAdress)
        {
            this.peerID = senderID;
            this.peerAdress = peerAdress;
            this.last_updated = DateTime.Now;    
        }

        [SerializeField] private string _peerID;
        [SerializeField] private string _peerAdress;
        [SerializeField] private bool _requestedByLocal;
        [SerializeField] private float _ping;
        [SerializeField] private float _score;
        [SerializeField] private float _trust_coefficient;
        [SerializeField] private DateTime _last_updated;

        public string peerID {  get => _peerID; set => _peerID = value; } 
        public string peerAdress { get => _peerAdress; set => _peerAdress = value; }

        /// <summary>
        /// true when the local node is the one who requested the connection with the peer represennted by the instance of peerinfo
        /// only requesters handle the heartbeat of the connection
        /// </summary>
        public bool requestedByLocal { get => _requestedByLocal; set => _requestedByLocal = value; }

        /// <summary>
        /// average ping
        /// </summary>
        public float averagePing { get => _ping; set => _ping = value; }
        [ReadOnly] private int _pingCompoung = 0;
        public float score { get => _score; set => _score = value; }

        /// <summary>
        ///  form -100 to 100, an indice of trust that a node is begin attributed by the network over time
        ///  all new nodes begin at 0
        /// </summary>
        public float trust_coefficient { get => _trust_coefficient; set => _trust_coefficient = value; }

        /// <summary>
        /// For callers, the last time a heartbeat was received
        /// For Listenners (locally), the last time local node sent a heartbeat
        /// </summary>
        public DateTime last_updated { get => _last_updated; set => _last_updated = value; }
        public DateTime last_received { get => _last_updated; set => _last_updated = value; }

        public void SetScoreByDistance(Vector3 localPosition)
        {
            var dist = Vector3.Distance(WorldSimulationManager.nodeAddresses[peerAdress].transform.position, localPosition);
            score = 1f / dist * 100f;
        }

        public void UpdateAveragePing(int requestping)
        {
            var deaggregateVal = averagePing * _pingCompoung;
            _pingCompoung++;
            averagePing = (deaggregateVal + requestping) * _pingCompoung;
        }

        public float ComputeScore(float ping, int listennersCount)
        {
            /*// the more ping the less score
            float pingratio = ping;
            // the less connection the more score (balancing the pingratio) => new comers with low connections have more chances to get new connections
            float connectivity = (1f / (listennersCount + 1)) * 100f;

            float score = connectivity / pingratio;

            this.ping = pingratio;
            //this.score = score;
            //this.score = 1 / ping * 100f;

            this.last_updated = DateTime.UtcNow;*/
            return score;
        }

        public void SetScore(float score)
        {
            this.score = score;
            this.last_updated = DateTime.Now;
        }
    }
}
