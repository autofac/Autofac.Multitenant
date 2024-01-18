// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Autofac.Core.Lifetime;
using Autofac.Core.Resolving;
using Autofac.Multitenant.Test.Stubs;

namespace Autofac.Multitenant.Test;

public class MultitenantContainerFixture
{
    [Fact]
    public void BeginLifetimeScope_ChildScopeCanBeConfigured()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>());
        using (var nestedScope = mtc.BeginLifetimeScope(b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>()))
        {
            var nestedDependency = nestedScope.Resolve<IStubDependency1>();
            Assert.IsType<StubDependency1Impl2>(nestedDependency);
        }
    }

    [Fact]
    public void BeginLifetimeScope_ChildScopeCanBeConfiguredAndTagged()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>());
        using (var nestedScope = mtc.BeginLifetimeScope("tag", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>()))
        {
            Assert.Equal("tag", nestedScope.Tag);
            var nestedDependency = nestedScope.Resolve<IStubDependency1>();
            Assert.IsType<StubDependency1Impl2>(nestedDependency);
        }
    }

    [Fact]
    public void BeginLifetimeScope_ChildScopeCanBeTagged()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        using (var nestedScope = mtc.BeginLifetimeScope("tag"))
        {
            Assert.Equal("tag", nestedScope.Tag);
        }
    }

    [Fact]
    public void BeginLifetimeScope_CreatesLifetimeScopeForCurrentTenant()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>().InstancePerLifetimeScope());
        var tenantScope = mtc.GetCurrentTenantScope();
        var tenantDependency = tenantScope.Resolve<IStubDependency1>();
        using (var nestedScope = mtc.BeginLifetimeScope())
        {
            var nestedDependency = nestedScope.Resolve<IStubDependency1>();
            Assert.NotSame(tenantDependency, nestedDependency);
        }
    }

    [Fact]
    public void ComponentRegistry_ReturnsRegistryFromCurrentTenantLifetimeScope()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        var scope = mtc.GetCurrentTenantScope();
        Assert.Same(scope.ComponentRegistry, mtc.ComponentRegistry);
    }

    [Fact]
    public void ConfigureTenant_DoesNotAllowMultipleSubsequentRegistrationsForDefaultTenant()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().As<IStubDependency1>();
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant(null, b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>());
        Assert.Throws<InvalidOperationException>(() => mtc.ConfigureTenant(null, b => b.RegisterType<StubDependency2Impl2>().As<IStubDependency2>()));
    }

    [Fact]
    public void ConfigureTenant_DoesNotAllowMultipleSubsequentRegistrationsForOneTenant()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().As<IStubDependency1>();
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>());
        Assert.Throws<InvalidOperationException>(() => mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency2Impl2>().As<IStubDependency2>()));
    }

    [Fact]
    public void ConfigureTenant_RequiresConfiguration()
    {
        var builder = new ContainerBuilder();
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        Assert.Throws<ArgumentNullException>(() => mtc.ConfigureTenant("tenant1", null));
    }

    [Fact]
    public void ConfigureTenant_ThrowsAfterDisposal()
    {
        var builder = new ContainerBuilder();
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => mtc.ConfigureTenant("tenant1", _ => { }));
    }

    [Fact]
    public void Ctor_NullApplicationContainer()
    {
        Assert.Throws<ArgumentNullException>(() => new MultitenantContainer(new StubTenantIdentificationStrategy(), null));
    }

    [Fact]
    public void Ctor_NullTenantIdentificationStrategy()
    {
        Assert.Throws<ArgumentNullException>(() => new MultitenantContainer(null, new ContainerBuilder().Build()));
    }

    [Fact]
    public void Ctor_SetsProperties()
    {
        var container = new ContainerBuilder().Build();
        var strategy = new StubTenantIdentificationStrategy();
        using var mtc = new MultitenantContainer(strategy, container);
        Assert.Same(container, mtc.ApplicationContainer);
        Assert.Same(strategy, mtc.TenantIdentificationStrategy);
    }

    [Fact]
    public void Dispose_DisposesTenantLifetimeScopes()
    {
        using var appDependency = new StubDisposableDependency();
        using var tenantDependency = new StubDisposableDependency();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(appDependency).OwnedByLifetimeScope();
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterInstance(tenantDependency).OwnedByLifetimeScope());

        // Resolve the tenant dependency so it's added to the list of things to dispose.
        // If you don't do this, it won't be queued for disposal and the test fails.
        mtc.Resolve<StubDisposableDependency>();

        mtc.Dispose();
        Assert.True(appDependency.IsDisposed, "The application scope didn't run Dispose.");
        Assert.True(tenantDependency.IsDisposed, "The tenant scope didn't run Dispose.");
    }

    [Fact]
    public void Disposer_ReturnsRegistryFromCurrentTenantLifetimeScope()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        var scope = mtc.GetCurrentTenantScope();
        Assert.Same(scope.Disposer, mtc.Disposer);
    }

    [Fact]
    public void GetCurrentTenantScope_ChangesByContext()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        var tenant1a = mtc.GetCurrentTenantScope();
        strategy.TenantId = "tenant2";
        var tenant2 = mtc.GetCurrentTenantScope();
        strategy.TenantId = "tenant1";
        var tenant1b = mtc.GetCurrentTenantScope();
        Assert.Same(tenant1a, tenant1b);
        Assert.NotSame(tenant1a, tenant2);
    }

    [Fact]
    public void GetCurrentTenantScope_TenantFound()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        var current = mtc.GetCurrentTenantScope();
        var tenant = mtc.GetTenantScope("tenant1");
        Assert.Same(tenant, current);
    }

    [Fact]
    public void GetCurrentTenantScope_TenantNotFound()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
            IdentificationSuccess = false,
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        var current = mtc.GetCurrentTenantScope();
        var tenant = mtc.GetTenantScope(null);
        Assert.Same(tenant, current);
    }

    [Fact]
    public void GetTenantScope_NullIsDefaultTenant()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        var scope = mtc.GetTenantScope(null);
        Assert.NotNull(scope);
        Assert.NotSame(mtc.ApplicationContainer, scope);
    }

    [Fact]
    public void GetTenantScope_GetsTenantScopeForConfiguredTenant()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>());
        var scope = mtc.GetTenantScope("tenant1");
        Assert.NotNull(scope);
        Assert.NotSame(mtc.ApplicationContainer, scope);
    }

    [Fact]
    public void GetTenantScope_GetsTenantScopeForUnconfiguredTenant()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        var scope = mtc.GetTenantScope("tenant1");
        Assert.NotNull(scope);
        Assert.NotSame(mtc.ApplicationContainer, scope);
    }

    [Fact]
    public void GetTenantScope_SubsequentRetrievalsGetTheSameLifetimeScope()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>());
        var scope1 = mtc.GetTenantScope("tenant1");
        var scope2 = mtc.GetTenantScope("tenant1");
        Assert.Same(scope1, scope2);
    }

    [Fact]
    public void Resolve_ApplicationLevelSingleton()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().As<IStubDependency1>().SingleInstance();
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());

        // Two resolutions for a single tenant
        var dep1 = mtc.Resolve<IStubDependency1>();
        var dep2 = mtc.Resolve<IStubDependency1>();

        // One resolution for a different tenant
        strategy.TenantId = "tenant2";
        var dep3 = mtc.Resolve<IStubDependency1>();

        Assert.Same(dep1, dep2);
        Assert.Same(dep1, dep3);
    }

    [Fact]
    public void Resolve_ResolvesTenantSpecificRegistrations()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().As<IStubDependency1>();
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl3>().As<IStubDependency1>());

        Assert.IsType<StubDependency1Impl2>(mtc.Resolve<IStubDependency1>());
        strategy.TenantId = "tenant2";
        Assert.IsType<StubDependency1Impl3>(mtc.Resolve<IStubDependency1>());
    }

    [Fact]
    public void Resolve_TenantFallbackToApplicationContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().As<IStubDependency1>();
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        Assert.IsType<StubDependency1Impl1>(mtc.Resolve<IStubDependency1>());
    }

    [Fact]
    public void Resolve_TenantLevelSingleton()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().As<IStubDependency1>().SingleInstance();

        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>().SingleInstance());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>().SingleInstance());

        // Get the application-level dependency
        var appLevel = mtc.ApplicationContainer.Resolve<IStubDependency1>();

        // Two resolutions for a single tenant
        var dep1 = mtc.Resolve<IStubDependency1>();
        var dep2 = mtc.Resolve<IStubDependency1>();

        // One resolution for a different tenant
        strategy.TenantId = "tenant2";
        var dep3 = mtc.Resolve<IStubDependency1>();

        Assert.IsType<StubDependency1Impl2>(dep1);
        Assert.IsType<StubDependency1Impl2>(dep3);
        Assert.IsType<StubDependency1Impl1>(appLevel);
        Assert.Same(dep1, dep2);
        Assert.NotSame(dep1, dep3);
        Assert.NotSame(dep1, appLevel);
    }

    [Fact]
    public void Tag_ReturnsRegistryFromCurrentTenantLifetimeScope()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        var scope = mtc.GetCurrentTenantScope();
        Assert.Same(scope.Tag, mtc.Tag);
    }

    [Fact]
    public void TenantIsConfigured_NotConfigured()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());

        Assert.False(mtc.TenantIsConfigured("tenant1"));
    }

    [Fact]
    public void TenantIsConfigured_Configured()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => { });

        Assert.True(mtc.TenantIsConfigured("tenant1"));
    }

    [Fact]
    public void TenantIsConfigured_DefaultConfigures()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.GetTenantScope("tenant1");

        Assert.True(mtc.TenantIsConfigured("tenant1"));
    }

    [Fact]
    public void RemoveTenant_ShowFallback()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces();
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());

        mtc.RemoveTenant("tenant1");

        Assert.IsType<StubDependency1Impl1>(mtc.Resolve<IStubDependency1>());
    }

    [Fact]
    public void RemoveTenant_ShowDisposal()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces());

        var tenant1scope = mtc.GetTenantScope("tenant1");

        mtc.RemoveTenant("tenant1");

        Assert.Throws<ObjectDisposedException>(() => tenant1scope.Resolve<IStubDependency1>());
    }

    [Fact]
    public void RemoveTenant_ConfigureAfterRemove()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces();
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());

        mtc.RemoveTenant("tenant1");

        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());

        Assert.IsType<StubDependency1Impl3>(mtc.Resolve<IStubDependency1>());
    }

    [Fact]
    public void RemoveTenant_ConfigureAfterRemoveFailCaseDemo()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces();
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());

        mtc.RemoveTenant("tenant1");

        // contextual tenant is still "tenant1"; this will force creation of container-configured tenant
        mtc.Resolve<IStubDependency1>();

        Assert.Throws<InvalidOperationException>(() => mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces()));
    }

    [Fact]
    public void RemoveTenant_TenantSingleton()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDisposableDependency>().SingleInstance());
        var stub = mtc.Resolve<StubDisposableDependency>();

        mtc.RemoveTenant("tenant1");

        Assert.True(stub.IsDisposed);
    }

    [Fact]
    public void RemoveTenant_ReturnsFalseWhenNotPresent()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDisposableDependency>().SingleInstance());

        var removed = mtc.RemoveTenant("tenant2");

        Assert.False(removed);
    }

    [Fact]
    public void ReconfigureTenant_Reconfigure()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());
        mtc.Resolve<IStubDependency1>();

        mtc.ReconfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());

        Assert.IsType<StubDependency1Impl3>(mtc.Resolve<IStubDependency1>());
    }

    [Fact]
    public void ReconfigureTenant_ReconfigureSingleton()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces().SingleInstance());
        mtc.Resolve<IStubDependency1>();

        mtc.ReconfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces().SingleInstance());

        Assert.IsType<StubDependency1Impl3>(mtc.Resolve<IStubDependency1>());
    }

    [Fact]
    public void GetTenants_CheckRegistered()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant3", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());

        var registeredTenants = mtc.GetTenants().ToList();
        Assert.Equal(3, registeredTenants.Count);

        foreach (var tenantId in registeredTenants)
        {
            var scope = mtc.GetTenantScope(tenantId);
            Assert.NotNull(scope);
        }
    }

    [Fact]
    public void ClearTenants_EnsureRegisteredTenantsCountIsZero()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces();
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant3", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());

        mtc.ClearTenants();

        Assert.Empty(mtc.GetTenants());
    }

    [Fact]
    public void ClearTenants_ShowFallback()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces();
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant3", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());

        var registeredTenants = mtc.GetTenants();
        mtc.ClearTenants();

        foreach (var tenantId in registeredTenants)
        {
            strategy.TenantId = tenantId;
            Assert.IsType<StubDependency1Impl1>(mtc.Resolve<IStubDependency1>());
        }
    }

    [Fact]
    public void ClearTenants_ShowDisposal()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant3", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());

        var registeredTenants = mtc.GetTenants();
        var tenantScopes = new List<ILifetimeScope>();
        foreach (var tenantId in registeredTenants)
        {
            tenantScopes.Add(mtc.GetTenantScope(tenantId));
        }

        mtc.ClearTenants();

        foreach (var tenantScope in tenantScopes)
        {
            Assert.Throws<ObjectDisposedException>(() => tenantScope.Resolve<IStubDependency1>());
        }
    }

    [Fact]
    public void ClearTenants_ConfigureAfterClearing()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces();
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant3", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());

        mtc.ClearTenants();

        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant3", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());

        Assert.IsType<StubDependency1Impl3>(mtc.Resolve<IStubDependency1>());
        strategy.TenantId = "tenant2";
        Assert.IsType<StubDependency1Impl2>(mtc.Resolve<IStubDependency1>());
        strategy.TenantId = "tenant3";
        Assert.IsType<StubDependency1Impl2>(mtc.Resolve<IStubDependency1>());
    }

    [Fact]
    public void ClearTenants_ConfigureAfterClearingFailCaseDemo()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var builder = new ContainerBuilder();
        builder.RegisterType<StubDependency1Impl1>().AsImplementedInterfaces();
        using var mtc = new MultitenantContainer(strategy, builder.Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());
        mtc.ConfigureTenant("tenant3", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces());

        mtc.ClearTenants();

        // contextual tenant is still "tenant1"; this will force creation of container-configured tenant
        mtc.Resolve<IStubDependency1>();
        Assert.Throws<InvalidOperationException>(() => mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl3>().AsImplementedInterfaces()));

        // contextual tenant is still "tenant2"; this will force creation of container-configured tenant
        strategy.TenantId = "tenant2";
        mtc.Resolve<IStubDependency1>();
        Assert.Throws<InvalidOperationException>(() => mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces()));

        // contextual tenant is still "tenant3"; this will force creation of container-configured tenant
        strategy.TenantId = "tenant3";
        mtc.Resolve<IStubDependency1>();
        Assert.Throws<InvalidOperationException>(() => mtc.ConfigureTenant("tenant3", b => b.RegisterType<StubDependency1Impl2>().AsImplementedInterfaces()));
    }

    [Fact]
    public void ClearTenants_TenantSingleton()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDisposableDependency>().SingleInstance());
        mtc.ConfigureTenant("tenant2", b => b.RegisterType<StubDisposableDependency>().SingleInstance());
        mtc.ConfigureTenant("tenant3", b => b.RegisterType<StubDisposableDependency>().SingleInstance());

        var registeredTenants = mtc.GetTenants();
        var tenantsStubs = new List<StubDisposableDependency>();
        foreach (var tenantId in registeredTenants)
        {
            strategy.TenantId = tenantId;
            tenantsStubs.Add(mtc.Resolve<StubDisposableDependency>());
        }

        mtc.ClearTenants();

        foreach (var tenantStub in tenantsStubs)
        {
            Assert.True(tenantStub.IsDisposed);
        }
    }

    [Fact]
    public async Task MultitenantContainer_AsyncDispose()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>());

        await mtc.DisposeAsync();
    }

    [Fact]
    public void MultitenantContainer_DiagnosticSourceNotNull()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };

        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());

        Assert.NotNull(mtc.DiagnosticSource);
    }

    [Fact]
    public void ResolveOperationBeginning_FiresWhenResolving()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>());

        Assert.Raises<ResolveOperationBeginningEventArgs>(
            e => mtc.ResolveOperationBeginning += e,
            e => mtc.ResolveOperationBeginning -= e,
            () => mtc.GetTenantScope("tenant1").Resolve<IStubDependency1>());
    }

    [Fact]
    public void ChildLifetimeScopeBeginning_FiresWhenChildScopeIsCreated()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>());

        Assert.Raises<LifetimeScopeBeginningEventArgs>(
            e => mtc.ChildLifetimeScopeBeginning += e,
            e => mtc.ChildLifetimeScopeBeginning -= e,
            () => mtc.GetTenantScope("tenant1").BeginLifetimeScope());
    }

    [Fact]
    public void CurrentScopeEnding_FiresWhenScopeDisposed()
    {
        var strategy = new StubTenantIdentificationStrategy()
        {
            TenantId = "tenant1",
        };
        using var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
        mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>());

        Assert.Raises<LifetimeScopeEndingEventArgs>(
            e => mtc.CurrentScopeEnding += e,
            e => mtc.CurrentScopeEnding -= e,
            () => mtc.RemoveTenant("tenant1"));
    }
}
