using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public interface IResponse
    {
        public long callerPacketUniqueId { get; set; }
    }
}
