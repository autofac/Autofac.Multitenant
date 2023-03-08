// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Autofac.Multitenant.Test.Stubs
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
