// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Autofac.Multitenant.Test.Stubs;

namespace Autofac.Multitenant.Test;

public class ConfigurationActionBuilderFixture
{
    [Fact]
    public void Build_NoActionsRegistered()
    {
        var builder = new ConfigurationActionBuilder();
        Assert.NotNull(builder.Build());
    }

    [Fact]
    public void Build_MultipleActionsRegistered()
    {
        var builder = new ConfigurationActionBuilder
        {
            b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>(),
            b => b.RegisterType<StubDependency2Impl1>().As<IStubDependency2>(),
        };
        var built = builder.Build();

        var container = new ContainerBuilder().Build();
        using (var scope = container.BeginLifetimeScope(built))
        {
            Assert.IsType<StubDependency1Impl1>(scope.Resolve<IStubDependency1>());
            Assert.IsType<StubDependency2Impl1>(scope.Resolve<IStubDependency2>());
        }
    }

    [Fact]
    public void Build_SingleActionRegistered()
    {
        var builder = new ConfigurationActionBuilder
        {
            b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>(),
        };
        var built = builder.Build();

        var container = new ContainerBuilder().Build();
        using (var scope = container.BeginLifetimeScope(built))
        {
            Assert.IsType<StubDependency1Impl1>(scope.Resolve<IStubDependency1>());
        }
    }
}
