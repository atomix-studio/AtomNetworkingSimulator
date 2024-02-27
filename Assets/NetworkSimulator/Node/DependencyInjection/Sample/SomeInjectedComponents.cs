using System;
using UnityEngine;

namespace Atom.DependencyProvider.Samples
{
    [Serializable]
    public class SomeInjectedComponentA
    {
        public string SomeString;
    }

    [Serializable]
    public class SomeInjectedComponentB : IDependencyCreatedCallbackHandler
    {
        public string SomeInt;

        public void OnDependencyInjected(dynamic context)
        {
            Debug.Log($"{nameof(SomeInjectedComponentB)} has been injected in the context {context}");
        }
    }
}
