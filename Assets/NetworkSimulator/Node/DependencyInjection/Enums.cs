using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.ComponentProvider
{
    public enum DependencyInjectionMode
    {
        // all dependencies marked in the
        Awake = 0,
        // onrequested only
        LazyLoading = 1
    }
}
