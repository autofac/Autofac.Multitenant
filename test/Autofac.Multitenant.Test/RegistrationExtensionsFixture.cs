// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Autofac.Builder;
using Autofac.Multitenant.Test.Stubs;

namespace Autofac.Multitenant.Test;

public class RegistrationExtensionsFixture
{
    [Fact]
    public void InstancePerTenant_NullRegistration()
    {
        IRegistrationBuilder<StubDependency1Impl1, ConcreteReflectionActivatorData, SingleRegistrationStyle> registration = null;
        Assert.Throws<ArgumentNullException>(() => registration.InstancePerTenant());
    }

    [Fact]
    public void InstancePerTenant_RespectsLifetimeScope()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().As<IStubDependency1>().InstancePerTenant();
        using var mtc = new MultitenantContainer(strategy, builder.Build());

        // Two resolutions for a single tenant
        var dep1 = mtc.Resolve<IStubDependency1>();
        var dep2 = mtc.Resolve<IStubDependency1>();

        // One resolution for a different tenant
        strategy.TenantId = "tenant2";
        var dep3 = mtc.Resolve<IStubDependency1>();

        Assert.Same(dep1, dep2);
        Assert.NotSame(dep1, dep3);
    }

    [Fact]
    public void InstancePerTenant_RootAndPerTenantDependencies()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency3>().As<IStubDependency3>().InstancePerTenant();
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>().InstancePerTenant());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>().InstancePerTenant());

        // Two resolutions for a single tenant
        var dep1 = mtc.Resolve<IStubDependency3>();
        var dep2 = mtc.Resolve<IStubDependency3>();

        // One resolution for a different tenant
        strategy.TenantId = "tenant2";
        var dep3 = mtc.Resolve<IStubDependency3>();

        Assert.Same(dep1, dep2);
        Assert.NotSame(dep1, dep3);
        Assert.Same(dep1.Dependency, dep2.Dependency);
        Assert.NotSame(dep1.Dependency, dep3.Dependency);
    }
}
