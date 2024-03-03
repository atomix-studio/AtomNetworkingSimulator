using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    /// <summary>
    /// Base attribute to allow the provider to inject the dependency in the field or property
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InjectAttribute : Attribute
    {
        [Flags]
        public enum InjectingOptions
        {
            None = 0,

            /// <summary>
            /// Will allow the provider to search for an instance of the component type in the scene with a GameObject.Find()
            /// This will work only if the inject type inherits from component.
            /// It will return a runtime error if not the case (... work in progress attribute validation)
            /// </summary>
            AllowFindGameObject = 0,
        }

        public enum DependencyScopes
        {
            /// <summary>
            /// The dependency will be binded to the injectionInstance if the injection is required by it.
            /// It can be overriden by a [ServiceController] initializing its own dependencies as they will be scoped to the controller.
            /// It can also be overriden by forcing a container to a injectDependencies call if made manually.
            /// </summary>
            DefaultSelf = 0,

            /// <summary>
            /// Force to self do the same but avoid a ServiceController to bind the instance at controller scope and the provider will create an instance of the dependency type for the injectionContext scope only
            /// </summary>
            ForceToSelf = 1,

            /// <summary>
            /// Bind any dependency to an object scope.  
            /// </summary>
            GameObject = 2,
        }

        public InjectingOptions InjectionOptions { get; set; }

        public DependencyScopes DependencyScope { get; set; }

        public ITypeInjectorDefinition TypeInjectorDefinition { get; set; } = null;

        public InjectAttribute() { DependencyScope = DependencyScopes.DefaultSelf; }

        /// <summary>
        /// Dependency scope parameter provides a rule for the provider when it creates the instances of the required classes.        /// 
        /// </summary>
        /// <param name="dependencyScope"></param>
        public InjectAttribute(DependencyScopes dependencyScope = DependencyScopes.DefaultSelf, InjectingOptions injectionOptions = InjectingOptions.None, ITypeInjectorDefinition injectorDefinition = null)
        {
            DependencyScope = dependencyScope;
            InjectionOptions = injectionOptions;
            TypeInjectorDefinition = injectorDefinition;
        }

    }
}
