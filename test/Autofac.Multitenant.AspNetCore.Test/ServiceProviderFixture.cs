// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Autofac.Extensions.DependencyInjection;
using Autofac.Multitenant.AspNetCore.Test.Stubs;
using Microsoft.Extensions.DependencyInjection;

namespace Autofac.Multitenant.AspNetCore.Test;

public class ServiceProviderFixture
{
    [Fact]
    public void GetService_ApplicationLevelSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStubDependency1, StubDependency1Impl1>();

        var builder = new ContainerBuilder();
        builder.Populate(services);

        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());

        using var serviceProvider = new AutofacServiceProvider(mtc);

        // Two resolutions for a single tenant
        var dep1 = serviceProvider.GetService<IStubDependency1>();
        var dep2 = serviceProvider.GetService<IStubDependency1>();

        // One resolution for a different tenant
        strategy.TenantId = "tenant2";
        var dep3 = serviceProvider.GetService<IStubDependency1>();

        Assert.Same(dep1, dep2);
        Assert.Same(dep1, dep3);
    }

    [Fact]
    public void GetService_ResolvesTenantSpecificRegistrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStubDependency1, StubDependency1Impl1>();

        var builder = new ContainerBuilder();
        builder.Populate(services);

        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl3>().As<IStubDependency1>());

        using var serviceProvider = new AutofacServiceProvider(mtc);

        Assert.IsType<StubDependency1Impl2>(serviceProvider.GetService<IStubDependency1>());
        strategy.TenantId = "tenant2";
        Assert.IsType<StubDependency1Impl3>(serviceProvider.GetService<IStubDependency1>());
    }

    [Fact]
    public void GetService_TenantFallbackToApplicationContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStubDependency1, StubDependency1Impl1>();

        var builder = new ContainerBuilder();
        builder.Populate(services);

        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());

        using var serviceProvider = new AutofacServiceProvider(mtc);

        Assert.IsType<StubDependency1Impl1>(serviceProvider.GetService<IStubDependency1>());
    }
}
