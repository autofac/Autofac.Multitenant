using System;
using System.Linq;

namespace Autofac.Multitenant.AspNetCore.Test.Stubs
{
    public class StubTenantIdentificationStrategy : ITenantIdentificationStrategy
    {
        public StubTenantIdentificationStrategy()
        {
            this.IdentificationSuccess = true;
        }

        public bool IdentificationSuccess { get; set; }

        public object TenantId { get; set; }

        public bool TryIdentifyTenant(out object tenantId)
        {
            tenantId = this.TenantId;
            return this.IdentificationSuccess;
        }
    }
}
