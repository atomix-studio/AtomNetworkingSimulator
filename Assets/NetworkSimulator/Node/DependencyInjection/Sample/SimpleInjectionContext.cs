using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.DependencyProvider.Samples
{
    [InjectionContext]
    public class SimpleInjectionContext : MonoBehaviour, IDependencyCreatedCallbackHandler
    {
        [InjectComponent, ShowInInspector, ReadOnly] private SomeInjectedComponentA _componentA;
        [InjectComponent, ShowInInspector, ReadOnly] public SomeInjectedComponentB componentB { get; private set; }

        private void Awake()
        {
            DependencyProvider.registerInjectionContextDependenciesAwakeCallback(typeof(SimpleInjectionContext), this);
            DependencyProvider.injectDependencies(this);
        }

        // will be called by provider when all dependencies are created
        // the context can then notify the dependencies by calling a custom initialisation method
        public void OnDependencyInjected(object dependencyInstance)
        {
            
        }

    }
}
