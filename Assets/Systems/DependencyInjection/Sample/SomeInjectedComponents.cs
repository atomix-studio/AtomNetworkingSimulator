using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atom.DependencyProvider.Samples
{
    [Serializable]
    public class SomeInjectedComponentA
    {
        public string SomeString;
        [Inject] public SomeInjectedComponentD SomeInjectedComponentD { get; set; }
    }

    [Serializable]
    public class SomeInjectedComponentB : IDependenciesInjectionCallback
    {
        [Inject] public SomeInjectedComponentA SomeInjectedComponentA { get; set; }
        public void OnDependencyInjected(List<object> context)
        {
            Debug.Log($"{nameof(SomeInjectedComponentB)} has been injected in the context {context}");
        }
    }

    [Serializable]
    public class SomeInjectedComponentC : IDependenciesInjectionCallback
    {
        public string SomeInt;
        [Inject] public SomeInjectedComponentA SomeInjectedComponentA { get; set; }
        [Inject] public SomeInjectedComponentD SomeInjectedComponentD { get; set; }

        public void OnDependencyInjected(List<object> context)
        {
            Debug.Log($"{nameof(SomeInjectedComponentB)} has been injected in the context {context}");
        }
    }

    [Serializable]
    public class SomeInjectedComponentD : IDependenciesInjectionCallback
    {
        public string SomeInt;
        [Inject] public SomeInjectedComponentB SomeInjectedComponentB { get; set; }

        public void OnDependencyInjected(List<object> context)
        {
            Debug.Log($"{nameof(SomeInjectedComponentB)} has been injected in the context {context}");
        }
    }
}
