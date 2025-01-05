using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CommunicationSystem
{
    public interface IClonablePacket
    {
        /// <summary>
        /// Returns a copy of the received packet but keeps the broadcast datas 
        /// </summary>
        /// <param name="received"></param>
        /// <returns></returns>
        public INetworkPacket ClonePacket(INetworkPacket received);
    }
}
