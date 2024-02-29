using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    /// <summary>
    /// This attribute will mark a class as a service dependency from a controller.
    /// This will prevent this type to be created outside of a controller scope.
    /// The provider will handle the instantiation of all services within the controller scope or any scope forced by the controller
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class InjectedServiceAttribute : Attribute
    {
        /// <summary>
        /// The type of controller this service will be
        /// </summary>
        public Type ControllerType { get; set; }
    }
}
