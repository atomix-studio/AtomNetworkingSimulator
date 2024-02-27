using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    /// <summary>
    /// An interface to allow injected instances to receive a callback when their context has been fully initialized
    /// This callback is the moment from where any other injected compnent within an injection context instance can be accessed 
    /// </summary>
    public interface IDependencyCreatedCallbackHandler
    {
        public void OnDependencyInjected(object context);
    }
}
