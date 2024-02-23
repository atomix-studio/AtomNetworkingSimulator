using Atom.CommunicationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.NetworkSimulator.Node.CommunicationSystem.Components.Router
{
    public class RoutingMiddleware
    {
        private Action<INetworkPacket> _onReceive;
        private Func<bool> _exitCallback;
    }
}
