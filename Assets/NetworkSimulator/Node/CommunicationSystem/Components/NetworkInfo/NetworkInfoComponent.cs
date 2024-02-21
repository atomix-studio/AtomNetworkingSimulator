using Atom.ComponentSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atom.CommunicationSystem
{
    [Serializable]
    public class NetworkInfoComponent :  INodeComponent
    {
        /// <summary>
        /// INFO of the local peer
        /// </summary>
        public PeerInfo LocalPeerInfo { get; private set; }

        /// <summary>
        /// Peers that are currently sending datas to the local node
        /// </summary>
        public Dictionary<string, PeerInfo> Callers { get; set; }

        /// <summary>
        /// Peers that the local node can sends data to
        /// </summary>
        public Dictionary<string, PeerInfo> Listenners { get; set; }

        public NodeEntity context { get; set ; }

        // Initialization could eventually handle the retrieving of previous known connections ?
        public void Initialize(PeerInfo localPeerInfo)
        {
            LocalPeerInfo = localPeerInfo;
            Callers = new Dictionary<string, PeerInfo>();
            Listenners = new Dictionary<string, PeerInfo>();
        }

        public void OnInitialize()
        {
        }
    }
}
