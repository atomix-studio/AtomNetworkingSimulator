using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.NetworkSimulator.Node.DependencyInjection.Interfaces
{
    public interface IAbstractDependencyProviderInjectionContextMiddleware
    {
        public void OnInjectedInContext(dynamic context);
    }
}
