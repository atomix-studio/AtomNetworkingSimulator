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
        [InjectComponent] private SomeInjectedComponentA _componentA;
        [InjectComponent] public SomeInjectedComponentB componentB { get; private set; }

        private void Awake()
        {
            DependencyProvider.injectDependencies(this);
        }
    }
}
