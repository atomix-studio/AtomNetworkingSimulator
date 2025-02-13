﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.DependencyProvider
{

    /// <summary>
    /// WORK IN PROGRESS
    /// 
    /// will be an abstraction for the Component Provider
    /// INodeComponent with NodeEntity context will become an implementation of thi
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [InjectionContext]
    public interface IDependencyService<T> : IDependency
    {
        // context of type will be injected by default in any class inherinting from this interface implemenation
        [Inject] public T controller { get; set; }
    }
}
