using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.ComponentProvider
{
    /// <summary>
    /// Mark a class as Singleton will limit is instanciation by the provider at 1 instance and let it be accessed statically
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SingletonAttribute : Attribute
    {
    }
}
