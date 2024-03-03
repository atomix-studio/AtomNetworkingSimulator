using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    public static class DependencyProviderExtensions
    {
        public static void InitializeDependencies(this object dependencyContext, object dependencyContainerOverride = null)
        {
            DependencyProvider.InjectDependencies(dependencyContext, dependencyContainerOverride);
        }
        
        /// <summary>
        /// Retrieves or create an instance of type T within the instance container scope
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instanceContainer"></param>
        /// <returns></returns>
        public static T GetDependency<T>(this object instanceContainer) where T : class
        {
            return (T)DependencyProvider.getOrCreate<T>(instanceContainer);
        }
    }
}
