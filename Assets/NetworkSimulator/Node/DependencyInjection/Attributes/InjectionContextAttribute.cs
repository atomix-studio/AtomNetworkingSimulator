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
        /// <summary>
        /// Forcing all inherited types from ForceInheritedTypesInjectionInContext to be injected whereever they can be in the InjectionContext.
        /// If there is no member avalaible to inject the instances in, the forced types instances remaining will be created and kept by the provider
        /// as Anonymous Dependencies
        /// </summary>
        public Type ForceInheritedTypesInjectionInContext { get; set; }

        /// <summary>
        /// Another way of doing the same thing
        /// </summary>
        public Type[] ForceRequiredTypesInjectionInContext { get; set; }

        public InjectionContextAttribute() { }

        /// <summary>
        /// Passing a type in this constructor parameter will force the provider to generate any type inherinting from 'forceInheritingDependenciesInjectionInContext'
        /// in each instance of the context
        /// </summary>
        /// <param name="forceInheritingDependenciesInjectionInContext"></param>
        public InjectionContextAttribute(Type forceChildrenTypesInjectionInContext)
        {
            ForceInheritedTypesInjectionInContext = forceChildrenTypesInjectionInContext;
        }

        public InjectionContextAttribute(params Type[] forceRequiredTypesInjectionInContext)
        {
            ForceRequiredTypesInjectionInContext = forceRequiredTypesInjectionInContext;
        }
    }
}
