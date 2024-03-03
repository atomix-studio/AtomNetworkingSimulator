using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    /// <summary>
    /// The type injector definition is a way to implement an override on the instanciation/reference to an object that needs to be injected.
    /// For instance, if I need to allow the Dependency Provider to get a reference of the instance of my player, which instanciation is handled by other systems,
    /// I can create a TypeInjectorDefinition on the type of my player and the dependency provider will get the reference from here to inject it in other services
    /// </summary>
    public interface ITypeInjectorDefinition
    {
        /// <summary>
        /// Add here your own object creation/obtention logic.
        /// Returning null in this method will automatically let the provider executes the default GetOrCreate logic
        /// </summary>
        /// <returns></returns>
        public object GetOrCreate();               
    }
}
