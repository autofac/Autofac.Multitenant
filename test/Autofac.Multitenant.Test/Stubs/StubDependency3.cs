// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Autofac.Multitenant.Test.Stubs;

public class StubDependency3 : IStubDependency3
{
    public StubDependency3(IStubDependency1 depends)
    {
        Dependency = depends;
    }

    public IStubDependency1 Dependency { get; private set; }
}
