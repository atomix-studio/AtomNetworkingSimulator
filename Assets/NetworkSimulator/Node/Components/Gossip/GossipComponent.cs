using Atom.DependencyProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.Gossip
{
    /// <summary>
    /// Basically, gossip component is a buffer for datas that have to be shared 
    /// Other systems can relay datas a 'gossipable datas' by giving GossipComponent a packet 
    /// </summary>
    public class GossipComponent : MonoBehaviour, INodeComponent
    {
        public NodeEntity controller { get ; set ; }

        /// <summary>
        /// number of gossip ticks per minute
        /// </summary>
        [SerializeField] private float _gossipRate = 3;

        private float _timer = 0;

        public void OnInitialize()
        {
            
        }

        void Update()
        {
            if (!controller.IsConnectedAndReady)
                return;
        }
    }
}
