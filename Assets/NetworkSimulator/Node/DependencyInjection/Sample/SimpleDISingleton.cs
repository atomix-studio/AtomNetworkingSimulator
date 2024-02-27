using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.DependencyProvider.Samples
{
    [Singleton, InjectionContext]
    public class SimpleDISingleton : MonoBehaviour
    {
        [InjectComponent] SimpleInjectionContext _context;

        private void Awake()
        {
            Debug.Log($"{nameof(SimpleDISingleton)} has been created !");
            
        }
    }
}
