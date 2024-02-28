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
    public class SimpleInjectionContext : MonoBehaviour, IDependenciesInjectionCallback
    {
        [Inject, ShowInInspector, ReadOnly] private SomeInjectedComponentA _componentA;
        [Inject, ShowInInspector, ReadOnly] public SomeInjectedComponentB componentB { get; private set; }
        [Inject] public SimpleDISingleton SimpleDISingleton;

        private void Awake()
        {
            DependencyProvider.registerInjectionContextDependenciesAwakeCallback(typeof(SimpleInjectionContext), this);
        }

        [Button]
        public void InjectToSelf()
        {
            DependencyProvider.InjectDependencies(this);
        }

        [Button]
        public void InjectToGameobject()
        {
            DependencyProvider.InjectDependencies(this, this.gameObject);
        }

        // will be called by provider when all dependencies are created
        // the context can then notify the dependencies by calling a custom initialisation method
        public void OnDependencyInjected(List<object> dependencyInstance)
        {

        }

    }
}
