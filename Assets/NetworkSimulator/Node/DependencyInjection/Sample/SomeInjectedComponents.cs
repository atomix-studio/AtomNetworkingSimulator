using UnityEngine;

namespace Atom.DependencyProvider.Samples
{
    public class SomeInjectedComponentA
    {
    }

    public class SomeInjectedComponentB : IDependencyInjectionContextInitializedCallback
    {
        public void OnInjectionContextInitialized(dynamic context)
        {
            Debug.Log($"{nameof(SomeInjectedComponentB)} has been injected in the context {context}");
        }
    }
}
