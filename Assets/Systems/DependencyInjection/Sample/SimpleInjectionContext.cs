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
    public class SimpleInjectionContext : MonoBehaviour
    {
        [Inject, ShowInInspector, ReadOnly] private SomeInjectedComponentA _componentA;
        [Inject, ShowInInspector, ReadOnly] public SomeInjectedComponentB componentB { get; private set; }
        [Inject] public SimpleDISingleton SimpleDISingleton;

        private void Awake()
        {
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

       
    }
}
