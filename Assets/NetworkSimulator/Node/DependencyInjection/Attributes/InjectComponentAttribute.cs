using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.ComponentProvider
{
    /// <summary>
    /// Base attribute to allow the provider to inject the dependency in the field or property
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InjectComponentAttribute : Attribute
    {
        public InjectComponentAttribute() { }

    }
}
