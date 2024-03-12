// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Autofac.Multitenant;

/// <summary>
/// Defines a provider that determines the current tenant ID from
/// execution context.
/// </summary>
public interface ITenantIdentificationStrategy
{
    /// <summary>
    /// Attempts to identify the tenant from the current execution context.
    /// </summary>
    /// <param name="tenantId">
    /// The current tenant identifier.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if the tenant could be identified; <see langword="false" />
    /// if not.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate", Justification = "Tenant identifiers are objects.")]
    bool TryIdentifyTenant(out object? tenantId);
}
