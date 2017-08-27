// This software is part of the Autofac IoC container
// Copyright © 2014 Autofac Contributors
// http://autofac.org
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Autofac.Core;
using Autofac.Core.Lifetime;
using Autofac.Core.Resolving;
using Autofac.Util;

namespace Autofac.Multitenant
{
    /// <summary>
    /// <see cref="Autofac.IContainer"/> implementation that provides the ability
    /// to register and resolve dependencies in a multitenant environment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This container implementation modifies the definition of the standard
    /// container implementation by returning values that are tenant-specific.
    /// For example, resolving a component via <see cref="Autofac.Multitenant.MultitenantContainer.ResolveComponent"/>
    /// will yield a resolution of the dependency for the current tenant, not
    /// from a global container/lifetime.
    /// </para>
    /// <para>
    /// The "current tenant ID" is resolved from an implementation of
    /// <see cref="Autofac.Multitenant.ITenantIdentificationStrategy"/>
    /// that is passed into the container during construction.
    /// </para>
    /// <para>
    /// The ability to remove (<see cref="RemoveTenant(object)"/>) or reconfigure
    /// (<see cref="ReconfigureTenant(object, Action{ContainerBuilder})"/> an
    /// active tenant exists.  However, it must still be noted that
    /// tenant lifetime scopes are immutable: once they are retrieved,
    /// configured, or an item is resolved, that tenant lifetime scope
    /// cannot be updated or otherwise changed. This is important since
    /// it means you need to configure your defaults and tenant overrides
    /// early, in application startup.
    /// </para>
    /// <para>
    /// Even when using <see cref="ReconfigureTenant(object, Action{ContainerBuilder})"/>, the
    /// existing tenant scope isn't modified, but is disposed and rebuilt.
    /// Any dependencies that were resolved from a removed scope will also
    /// be disposed.  You will need to account for this in your application.
    /// Depending on your architecture, it may require users to re-login or some
    /// other form of soft reset.
    /// </para>
    /// <para>
    /// If you do not configure a tenant lifetime scope for a tenant but resolve a
    /// tenant-specific dependency for that tenant, the lifetime scope
    /// will be implicitly created for you.
    /// </para>
    /// <para>
    /// You may explicitly create and configure a tenant lifetime scope
    /// using the <see cref="Autofac.Multitenant.MultitenantContainer.ConfigureTenant"/>
    /// method. If you need to perform some logic and build up the configuration
    /// for a tenant, you can do that using a <see cref="Autofac.Multitenant.ConfigurationActionBuilder"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="Autofac.Multitenant.ConfigurationActionBuilder"/>
    [DebuggerDisplay("Tag = {Tag}, IsDisposed = {IsDisposed}")]
    public class MultitenantContainer : Disposable, IContainer
    {
        /// <summary>
        /// Marker object-tag for the tenant-level lifetime scope.
        /// </summary>
        internal static readonly object TenantLifetimeScopeTag = "tenantLifetime";

        /// <summary>
        /// Marker object representing the default tenant ID.
        /// </summary>
        private readonly object _defaultTenantId = new DefaultTenantId();

        /// <summary>
        /// Dictionary containing the set of tenant-specific lifetime scopes. Key
        /// is <see cref="System.Object"/>, value is <see cref="Autofac.ILifetimeScope"/>.
        /// </summary>
        // Issue #280: Incorrect double-checked-lock pattern usage in MultitenantContainer.GetTenantScope
        private readonly Dictionary<object, ILifetimeScope> _tenantLifetimeScopes = new Dictionary<object, ILifetimeScope>();

        /// <summary>
        /// Reader-writer lock for locking modifications and initializations
        /// of tenant scopes.
        /// </summary>
        private readonly System.Threading.ReaderWriterLockSlim _readWriteLock = new System.Threading.ReaderWriterLockSlim();

        /// <summary>
        /// Initializes a new instance of the <see cref="Autofac.Multitenant.MultitenantContainer"/> class.
        /// </summary>
        /// <param name="tenantIdentificationStrategy">
        /// The strategy to use for identifying the current tenant.
        /// </param>
        /// <param name="applicationContainer">
        /// The application container from which tenant-specific lifetimes will
        /// be created.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if <paramref name="tenantIdentificationStrategy" /> or
        /// <paramref name="applicationContainer"/> is <see langword="null" />.
        /// </exception>
        public MultitenantContainer(ITenantIdentificationStrategy tenantIdentificationStrategy, IContainer applicationContainer)
        {
            if (tenantIdentificationStrategy == null)
            {
                throw new ArgumentNullException(nameof(tenantIdentificationStrategy));
            }

            if (applicationContainer == null)
            {
                throw new ArgumentNullException(nameof(applicationContainer));
            }

            this.TenantIdentificationStrategy = tenantIdentificationStrategy;
            this.ApplicationContainer = applicationContainer;
        }

        /// <summary>
        /// Fired when a new scope based on the current scope is beginning.
        /// </summary>
        public event EventHandler<LifetimeScopeBeginningEventArgs> ChildLifetimeScopeBeginning
        {
            add { this.GetCurrentTenantScope().ChildLifetimeScopeBeginning += value; }

            remove { this.GetCurrentTenantScope().ChildLifetimeScopeBeginning -= value; }
        }

        /// <summary>
        /// Fired when this scope is ending.
        /// </summary>
        public event EventHandler<LifetimeScopeEndingEventArgs> CurrentScopeEnding
        {
            add { this.GetCurrentTenantScope().CurrentScopeEnding += value; }

            remove { this.GetCurrentTenantScope().CurrentScopeEnding -= value; }
        }

        /// <summary>
        /// Fired when a resolve operation is beginning in this scope.
        /// </summary>
        public event EventHandler<ResolveOperationBeginningEventArgs> ResolveOperationBeginning
        {
            add { this.GetCurrentTenantScope().ResolveOperationBeginning += value; }

            remove { this.GetCurrentTenantScope().ResolveOperationBeginning -= value; }
        }

        /// <summary>
        /// Gets the base application container.
        /// </summary>
        /// <value>
        /// An <see cref="Autofac.IContainer"/> on which all tenant lifetime
        /// scopes will be based.
        /// </value>
        public IContainer ApplicationContainer { get; private set; }

        /// <summary>
        /// Gets the current tenant's registry that associates services with the
        /// components that provide them.
        /// </summary>
        /// <value>
        /// An <see cref="Autofac.Core.IComponentRegistry"/> based on the current
        /// tenant context.
        /// </value>
        public IComponentRegistry ComponentRegistry
        {
            get { return this.GetCurrentTenantScope().ComponentRegistry; }
        }

        /// <summary>
        /// Gets the disposer associated with the current tenant's <see cref="T:Autofac.ILifetimeScope"/>.
        /// Component instances can be associated with it manually if required.
        /// </summary>
        /// <value>
        /// An <see cref="Autofac.Core.IDisposer"/> used in cleaning up component
        /// instances for the current tenant.
        /// </value>
        /// <remarks>
        /// Typical usage does not require interaction with this member - it
        /// is used when extending the container.
        /// </remarks>
        public IDisposer Disposer
        {
            get { return this.GetCurrentTenantScope().Disposer; }
        }

        /// <summary>
        /// Gets the tag applied to the current tenant's <see cref="T:Autofac.ILifetimeScope"/>.
        /// </summary>
        /// <value>
        /// An <see cref="System.Object"/> that identifies the current tenant's
        /// lifetime scope.
        /// </value>
        /// <remarks>
        /// Tags allow a level in the lifetime hierarchy to be identified.
        /// In most applications, tags are not necessary.
        /// </remarks>
        /// <seealso cref="M:Autofac.Builder.IRegistrationBuilder{T, U, V}.InstancePerMatchingLifetimeScope(System.Object)"/>
        public object Tag
        {
            get { return this.GetCurrentTenantScope().Tag; }
        }

        /// <summary>
        /// Gets the strategy used for identifying the current tenant.
        /// </summary>
        /// <value>
        /// An <see cref="Autofac.Multitenant.ITenantIdentificationStrategy"/>
        /// used to identify the current tenant from the execution context.
        /// </value>
        public ITenantIdentificationStrategy TenantIdentificationStrategy { get; private set; }

        /// <summary>
        /// Begin a new nested scope for the current tenant. Component instances created via the new scope
        /// will be disposed along with it.
        /// </summary>
        /// <returns>A new lifetime scope.</returns>
        public ILifetimeScope BeginLifetimeScope()
        {
            return this.GetCurrentTenantScope().BeginLifetimeScope();
        }

        /// <summary>
        /// Begin a new nested scope for the current tenant. Component instances created via the new scope
        /// will be disposed along with it.
        /// </summary>
        /// <param name="tag">The tag applied to the <see cref="T:Autofac.ILifetimeScope"/>.</param>
        /// <returns>A new lifetime scope.</returns>
        public ILifetimeScope BeginLifetimeScope(object tag)
        {
            return this.GetCurrentTenantScope().BeginLifetimeScope(tag);
        }

        /// <summary>
        /// Begin a new nested scope for the current tenant, with additional
        /// components available to it. Component instances created via the new scope
        /// will be disposed along with it.
        /// </summary>
        /// <param name="configurationAction">
        /// Action on a <see cref="T:Autofac.ContainerBuilder"/>
        /// that adds component registrations visible only in the new scope.
        /// </param>
        /// <returns>A new lifetime scope.</returns>
        /// <remarks>
        /// The components registered in the sub-scope will be treated as though they were
        /// registered in the root scope, i.e., SingleInstance() components will live as long
        /// as the root scope.
        /// </remarks>
        public ILifetimeScope BeginLifetimeScope(Action<ContainerBuilder> configurationAction)
        {
            return this.GetCurrentTenantScope().BeginLifetimeScope(configurationAction);
        }

        /// <summary>
        /// Begin a new nested scope for the current tenant, with additional
        /// components available to it. Component instances created via the new scope
        /// will be disposed along with it.
        /// </summary>
        /// <param name="tag">
        /// The tag applied to the <see cref="T:Autofac.ILifetimeScope"/>.
        /// </param>
        /// <param name="configurationAction">
        /// Action on a <see cref="T:Autofac.ContainerBuilder"/>
        /// that adds component registrations visible only in the new scope.
        /// </param>
        /// <returns>A new lifetime scope.</returns>
        /// <remarks>
        /// The components registered in the sub-scope will be treated as though they were
        /// registered in the root scope, i.e., SingleInstance() components will live as long
        /// as the root scope.
        /// </remarks>
        public ILifetimeScope BeginLifetimeScope(object tag, Action<ContainerBuilder> configurationAction)
        {
            return this.GetCurrentTenantScope().BeginLifetimeScope(tag, configurationAction);
        }

        /// <summary>
        /// Allows configuration of tenant-specific components. You may only call this
        /// method if the tenant is not currently configured.
        /// </summary>
        /// <param name="tenantId">
        /// The ID of the tenant for which configuration is occurring. If this
        /// value is <see langword="null" />, configuration occurs for the "default
        /// tenant" - the tenant that is used when no tenant ID can be determined.
        /// </param>
        /// <param name="configuration">
        /// An action that uses a <see cref="Autofac.ContainerBuilder"/> to set
        /// up registrations for the tenant.
        /// </param>
        /// <remarks>
        /// <para>
        /// If you need to configure a tenant across multiple registration
        /// calls, consider using a <see cref="Autofac.Multitenant.ConfigurationActionBuilder"/>
        /// and configuring the tenant using the aggregate configuration
        /// action it produces.
        /// </para>
        /// <para>
        /// Note that if <see cref="GetTenantScope(object)"/> is called using the tenant ID,
        /// it builds the tenant scope with the default (container) configuration, which will also
        /// preclude the tenant from being configured.  This includes the case where a dependency
        /// is resolved from the <see cref="MultitenantContainer"/> when the tenant ID is
        /// returned by the registered <see cref="TenantIdentificationStrategy"/>. If configuration
        /// can occur after application startup, use <see cref="ReconfigureTenant(object, Action{ContainerBuilder})"/>
        /// or "lock out" un-configured tenants using <see cref="TenantIsConfigured(object)"/> or
        /// other mechanism.
        /// </para>
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if <paramref name="configuration" /> is <see langword="null" />.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the tenant indicated by <paramref name="tenantId" />
        /// has already been configured.
        /// </exception>
        /// <seealso cref="Autofac.Multitenant.ConfigurationActionBuilder"/>
        /// <seealso cref="ReconfigureTenant(object, Action{ContainerBuilder})"/>
        public void ConfigureTenant(object tenantId, Action<ContainerBuilder> configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (tenantId == null)
            {
                tenantId = this._defaultTenantId;
            }

            _readWriteLock.EnterUpgradeableReadLock();
            try
            {
                if (this._tenantLifetimeScopes.ContainsKey(tenantId))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Properties.Resources.MultitenantContainer_TenantAlreadyConfigured, tenantId));
                }

                _readWriteLock.EnterWriteLock();
                try
                {
                    this._tenantLifetimeScopes[tenantId] = this.ApplicationContainer.BeginLifetimeScope(TenantLifetimeScopeTag, configuration);
                }
                finally
                {
                    _readWriteLock.ExitWriteLock();
                }
            }
            finally
            {
                _readWriteLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Allows re-configuration of tenant-specific components by closing and rebuilding
        /// the tenant scope.
        /// </summary>
        /// <param name="tenantId">
        /// The ID of the tenant for which configuration is occurring. If this
        /// value is <see langword="null" />, configuration occurs for the "default
        /// tenant" - the tenant that is used when no tenant ID can be determined.
        /// </param>
        /// <param name="configuration">
        /// An action that uses a <see cref="Autofac.ContainerBuilder"/> to set
        /// up registrations for the tenant.
        /// </param>
        /// <remarks>
        /// <para>
        /// If you need to configure a tenant across multiple registration
        /// calls, consider using a <see cref="Autofac.Multitenant.ConfigurationActionBuilder"/>
        /// and configuring the tenant using the aggregate configuration
        /// action it produces.
        /// </para>
        /// <para>
        /// This method is intended for use after application start-up.  During start-up, please
        /// use <see cref="ConfigureTenant(object, Action{ContainerBuilder})"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if <paramref name="configuration" /> is <see langword="null" />.
        /// </exception>
        /// <returns><c>true</c> if an existing configuration was removed; otherwise, <c>false</c>.</returns>
        /// <seealso cref="Autofac.Multitenant.ConfigurationActionBuilder"/>
        /// <seealso cref="ConfigureTenant(object, Action{ContainerBuilder})"/>
        public bool ReconfigureTenant(object tenantId, Action<ContainerBuilder> configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (tenantId == null)
            {
                tenantId = this._defaultTenantId;
            }

            // we're going to change the dictionary either way, dispense with the read-check
            _readWriteLock.EnterWriteLock();
            try
            {
                var removed = false;
                if (this._tenantLifetimeScopes.TryGetValue(tenantId, out var tenantScope) && tenantScope != null)
                {
                    tenantScope.Dispose();

                    removed = _tenantLifetimeScopes.Remove(tenantId);
                }

                this._tenantLifetimeScopes[tenantId] = this.ApplicationContainer.BeginLifetimeScope(TenantLifetimeScopeTag, configuration);

                return removed;
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Retrieves the lifetime scope for the current tenant based on execution
        /// context.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method uses the <see cref="Autofac.Multitenant.MultitenantContainer.TenantIdentificationStrategy"/>
        /// to retrieve the current tenant ID and then retrieves the scope
        /// using <see cref="Autofac.Multitenant.MultitenantContainer.GetTenantScope"/>.
        /// </para>
        /// </remarks>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "The results of this method change based on execution context.")]
        public ILifetimeScope GetCurrentTenantScope()
        {
            var tenantId = (object)null;
            if (this.TenantIdentificationStrategy.TryIdentifyTenant(out tenantId))
            {
                return this.GetTenantScope(tenantId);
            }

            return this.GetTenantScope(null);
        }

        /// <summary>
        /// Retrieves the lifetime scope for a specific tenant.
        /// </summary>
        /// <param name="tenantId">
        /// The ID of the tenant for which the lifetime scope should be retrieved. If this
        /// value is <see langword="null" />, the scope is returned for the "default
        /// tenant" - the tenant that is used when no tenant ID can be determined.
        /// </param>
        public ILifetimeScope GetTenantScope(object tenantId)
        {
            if (tenantId == null)
            {
                tenantId = this._defaultTenantId;
            }

            var tenantScope = (ILifetimeScope)null;
            _readWriteLock.EnterReadLock();
            try
            {
                this._tenantLifetimeScopes.TryGetValue(tenantId, out tenantScope);
            }
            finally
            {
                _readWriteLock.ExitReadLock();
            }

            if (tenantScope == null)
            {
                // just go straight to write-lock, chances of not needing it at this point would be low
                _readWriteLock.EnterWriteLock();

                try
                {
                    // The check and [potential] scope creation are locked here to
                    // ensure atomicity. We don't want to check and then have another
                    // thread create the lifetime scope behind our backs.
                    if (!this._tenantLifetimeScopes.TryGetValue(tenantId, out tenantScope) || tenantScope == null)
                    {
                        tenantScope = this.ApplicationContainer.BeginLifetimeScope(TenantLifetimeScopeTag);
                        this._tenantLifetimeScopes[tenantId] = tenantScope;
                    }
                }
                finally
                {
                    _readWriteLock.ExitWriteLock();
                }
            }

            return tenantScope;
        }

        /// <summary>
        /// Returns collection of all registered tenant IDs.
        /// </summary>
        public ICollection<object> GetTenantsIds()
        {
            _readWriteLock.EnterReadLock();
            try
            {
                return new List<object>(_tenantLifetimeScopes.Keys);
            }
            finally
            {
                _readWriteLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns whether the given tenant ID has been configured.
        /// </summary>
        /// <param name="tenantId">The tenant ID to test.</param>
        /// <returns>If configured, <c>true</c>; otherwise <c>false</c>.</returns>
        public bool TenantIsConfigured(object tenantId)
        {
            _readWriteLock.EnterReadLock();
            try
            {
                if (tenantId == null)
                {
                    tenantId = this._defaultTenantId;
                }

                return _tenantLifetimeScopes.ContainsKey(tenantId);
            }
            finally
            {
                _readWriteLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Removes the tenant configuration and disposes the associated lifetime scope.
        /// </summary>
        /// <param name="tenantId">The ID of the tenant to dispose.</param>
        /// <returns><c>true</c> if the tenant-collection was modified; otherwise, <c>false</c>.</returns>
        public bool RemoveTenant(object tenantId)
        {
            if (tenantId == null)
            {
                tenantId = this._defaultTenantId;
            }

            // this should be a fairly rare operation, so we'll jump right to the write-lock
            _readWriteLock.EnterWriteLock();
            try
            {
                if (this._tenantLifetimeScopes.TryGetValue(tenantId, out var tenantScope) && tenantScope != null)
                {
                    tenantScope.Dispose();

                    return _tenantLifetimeScopes.Remove(tenantId);
                }

                return false;
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Clears all tenants configurations and disposes the associated lifetime scopes.
        /// </summary>
        public void ClearTenants()
        {
            _readWriteLock.EnterWriteLock();
            try
            {
                foreach (var tenantScope in _tenantLifetimeScopes.Values)
                {
                    tenantScope.Dispose();
                }

                _tenantLifetimeScopes.Clear();
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Resolve an instance of the provided registration within the current tenant context.
        /// </summary>
        /// <param name="registration">The registration to resolve.</param>
        /// <param name="parameters">Parameters for the instance.</param>
        /// <returns>The component instance.</returns>
        /// <exception cref="T:Autofac.Core.Registration.ComponentNotRegisteredException">
        /// Thrown if an attempt is made to resolve a component that is not registered
        /// for the current tenant.
        /// </exception>
        /// <exception cref="T:Autofac.Core.DependencyResolutionException">
        /// Thrown if there is a problem resolving the registration. For example,
        /// if the component registered requires another component be available
        /// but that required component is not available, this exception will be thrown.
        /// </exception>
        public object ResolveComponent(IComponentRegistration registration, IEnumerable<Parameter> parameters)
        {
            return this.GetCurrentTenantScope().ResolveComponent(registration, parameters);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true" /> to release both managed and unmanaged resources;
        /// <see langword="false" /> to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Lock the lifetime scope table so no threads can add new lifetime
                // scopes while we're disposing.
                _readWriteLock.EnterWriteLock();

                try
                {
                    foreach (var scope in this._tenantLifetimeScopes.Values)
                    {
                        scope.Dispose();
                    }

                    this.ApplicationContainer.Dispose();
                }
                finally
                {
                    _readWriteLock.ExitWriteLock();
                }

                _readWriteLock.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
