using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    /// <summary>
    /// Mark a class or an interface to allow it to be detected by the dependency injection system
    /// If members in class are marked with InjectComponent but the class is not inheriting from InjectionContext, DI won't work
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    internal class InjectionContextAttribute : Attribute
    {
    }
}
