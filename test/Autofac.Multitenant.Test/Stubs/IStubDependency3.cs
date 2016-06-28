using System;
using System.Linq;

namespace Autofac.Multitenant.Test.Stubs
{
    /// <summary>
    /// Has a dependency on <see cref="IStubDependency1"/>
    /// </summary>
    public interface IStubDependency3
    {
        IStubDependency1 Dependency { get; }
    }
}
