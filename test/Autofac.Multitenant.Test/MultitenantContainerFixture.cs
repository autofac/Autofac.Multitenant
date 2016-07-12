﻿using System;
using Autofac;
using Autofac.Multitenant;
using Autofac.Multitenant.Test.Stubs;
using Xunit;

namespace Autofac.Multitenant.Test
{
    public class MultitenantContainerFixture
    {
        [Fact]
        public void BeginLifetimeScope_ChildScopeCanBeConfigured()
        {
            var strategy = new StubTenantIdentificationStrategy()
            {
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, builder.Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, builder.Build());
            mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency1Impl2>().As<IStubDependency1>());
            Assert.Throws<InvalidOperationException>(() => mtc.ConfigureTenant("tenant1", b => b.RegisterType<StubDependency2Impl2>().As<IStubDependency2>()));
        }

        [Fact]
        public void ConfigureTenant_RequiresConfiguration()
        {
            var builder = new ContainerBuilder();
            var strategy = new StubTenantIdentificationStrategy()
            {
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, builder.Build());
            Assert.Throws<ArgumentNullException>(() => mtc.ConfigureTenant("tenant1", null));
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
            var mtc = new MultitenantContainer(strategy, container);
            Assert.Same(container, mtc.ApplicationContainer);
            Assert.Same(strategy, mtc.TenantIdentificationStrategy);
        }

        [Fact]
        public void Dispose_DisposesTenantLifetimeScopes()
        {
            var appDependency = new StubDisposableDependency();
            var tenantDependency = new StubDisposableDependency();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(appDependency).OwnedByLifetimeScope();
            var strategy = new StubTenantIdentificationStrategy()
            {
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, builder.Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
            var scope = mtc.GetCurrentTenantScope();
            Assert.Same(scope.Disposer, mtc.Disposer);
        }

        [Fact]
        public void GetCurrentTenantScope_ChangesByContext()
        {
            var strategy = new StubTenantIdentificationStrategy()
            {
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
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
                IdentificationSuccess = false
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
            var current = mtc.GetCurrentTenantScope();
            var tenant = mtc.GetTenantScope(null);
            Assert.Same(tenant, current);
        }

        [Fact]
        public void GetTenantScope_NullIsDefaultTenant()
        {
            var strategy = new StubTenantIdentificationStrategy()
            {
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
            var scope = mtc.GetTenantScope(null);
            Assert.NotNull(scope);
            Assert.NotSame(mtc.ApplicationContainer, scope);
        }

        [Fact]
        public void GetTenantScope_GetsTenantScopeForConfiguredTenant()
        {
            var strategy = new StubTenantIdentificationStrategy()
            {
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
            var scope = mtc.GetTenantScope("tenant1");
            Assert.NotNull(scope);
            Assert.NotSame(mtc.ApplicationContainer, scope);
        }

        [Fact]
        public void GetTenantScope_SubsequentRetrievalsGetTheSameLifetimeScope()
        {
            var strategy = new StubTenantIdentificationStrategy()
            {
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, builder.Build());

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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, builder.Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, builder.Build());
            Assert.IsType<StubDependency1Impl1>(mtc.Resolve<IStubDependency1>());
        }

        [Fact]
        public void Resolve_TenantLevelSingleton()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<StubDependency1Impl1>().As<IStubDependency1>().SingleInstance();

            var strategy = new StubTenantIdentificationStrategy()
            {
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, builder.Build());
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
                TenantId = "tenant1"
            };
            var mtc = new MultitenantContainer(strategy, new ContainerBuilder().Build());
            var scope = mtc.GetCurrentTenantScope();
            Assert.Same(scope.Tag, mtc.Tag);
        }
    }
}