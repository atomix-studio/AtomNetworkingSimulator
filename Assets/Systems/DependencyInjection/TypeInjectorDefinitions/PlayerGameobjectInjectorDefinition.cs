using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{
    public class PlayerEntity
    {
        public string Name;
    }

    public class PlayerInjectorDefinition : ITypeInjectorDefinition
    {
        public object GetOrCreate()
        {
            return new PlayerEntity() { Name = "MyPlayer" };
        }
    }
}