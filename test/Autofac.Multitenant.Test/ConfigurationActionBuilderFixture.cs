using System;
using Autofac;
using Autofac.Multitenant;
using Autofac.Multitenant.Test.Stubs;
using Xunit;

namespace Autofac.Multitenant.Test
{
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
            var builder = new ConfigurationActionBuilder();
            builder.Add(b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>());
            builder.Add(b => b.RegisterType<StubDependency2Impl1>().As<IStubDependency2>());
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
            var builder = new ConfigurationActionBuilder();
            builder.Add(b => b.RegisterType<StubDependency1Impl1>().As<IStubDependency1>());
            var built = builder.Build();

            var container = new ContainerBuilder().Build();
            using (var scope = container.BeginLifetimeScope(built))
            {
                Assert.IsType<StubDependency1Impl1>(scope.Resolve<IStubDependency1>());
            }
        }
    }
}
