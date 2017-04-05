using System;
using Autofac;
using Autofac.Multitenant;
using Autofac.Multitenant.Test.Stubs;
using Xunit;

namespace Autofac.Multitenant.Test
{
    public class TenantIdentificationStrategyExtensionsFixture
    {
        [Fact]
        public void IdentifyTenant_FailedConversion()
        {
            var strategy = new StubTenantIdentificationStrategy
            {
                TenantId = Guid.NewGuid(),
            };
            Assert.Throws<InvalidCastException>(() => strategy.IdentifyTenant<int>());
        }

        [Fact]
        public void IdentifyTenant_FailedRetrieval()
        {
            var strategy = new StubTenantIdentificationStrategy
            {
                IdentificationSuccess = false,
            };
            Assert.Equal(Guid.Empty, strategy.IdentifyTenant<Guid>());
        }

        [Fact]
        public void IdentifyTenant_NullStrategy()
        {
            ITenantIdentificationStrategy strategy = null;
            Assert.Throws<ArgumentNullException>(() => strategy.IdentifyTenant<Guid>());
        }

        [Fact]
        public void IdentifyTenant_SuccessfulRetrieval()
        {
            var expected = Guid.NewGuid();
            var strategy = new StubTenantIdentificationStrategy
            {
                TenantId = expected,
            };
            Assert.Equal(expected, strategy.IdentifyTenant<Guid>());
        }
    }
}
