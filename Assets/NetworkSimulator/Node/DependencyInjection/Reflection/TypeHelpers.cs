using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;

namespace Atom.DependencyProvider
{
    public static class TypeHelpers
    {
        public static bool ImplementsInterface<T>(Type tocheck)
        {
            return typeof(T).IsAssignableFrom(tocheck);
        }
    }
}
