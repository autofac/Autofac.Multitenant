// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Autofac.Multitenant.Test.Stubs
{
    /// <summary>
    /// Has a dependency on <see cref="IStubDependency1"/>.
    /// </summary>
    public interface IStubDependency3
    {
        IStubDependency1 Dependency { get; }
    }
}
