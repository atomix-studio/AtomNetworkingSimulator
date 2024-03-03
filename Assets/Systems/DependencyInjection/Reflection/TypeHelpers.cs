using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;

namespace Atom.DependencyProvider
{
    public static class TypeHelpers
    {
        public static bool ImplementsInterface<T>(Type tocheck)
        {
            return typeof(T).IsAssignableFrom(tocheck);
        }

        public static List<Type> GetInheritingTypes(Type parentType, List<Type> types)
        {
            var result = new List<Type>();  
            
            for(int i = 0; i < types.Count; ++i)
            {
                if (types[i].IsAbstract)
                    continue;

                if (parentType.IsAssignableFrom(types[i]) || types[i] == parentType)
                {
                    result.Add(types[i]);
                }
            }
            return result;
        }
    }
}
