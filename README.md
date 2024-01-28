# Autofac.Multitenant

Multitenant application support for [Autofac IoC](https://github.com/autofac/Autofac).

[![Build status](https://ci.appveyor.com/api/projects/status/9120t73i97ywdoav?svg=true)](https://ci.appveyor.com/project/Autofac/autofac-multitenant) [![codecov](https://codecov.io/gh/Autofac/Autofac.Multitenant/branch/develop/graph/badge.svg)](https://codecov.io/gh/Autofac/Autofac.Multitenant)

Please file issues and pull requests for this package in this repository rather than in the Autofac core repo.

**BREAKING CHANGE**: As of v4.0.0, the `Autofac.Extras.Multitenant` package is `Autofac.Multitenant`.

- [Documentation](https://autofac.readthedocs.io/en/latest/advanced/multitenant.html)
- [NuGet](https://www.nuget.org/packages/Autofac.Multitenant)
- [Contributing](https://autofac.readthedocs.io/en/latest/contributors.html)
- [Open in Visual Studio Code](https://open.vscode.dev/autofac/Autofac.Multitenant)

## Quick Start

```c#
// First, create your application-level defaults using a standard
// ContainerBuilder, just as you are used to.
var builder = new ContainerBuilder();
builder.RegisterType<Consumer>()
  .As<IDependencyConsumer>()
  .InstancePerDependency();
builder.RegisterType<BaseDependency>()
  .As<IDependency>()
  .SingleInstance();
var appContainer = builder.Build();

// Once you've built the application-level default container, you
// need to create a tenant identification strategy that implements
// ITenantIdentificationStrategy. This is how the container will
// figure out which tenant is acting and needs dependencies resolved.
var tenantIdentifier = new MyTenantIdentificationStrategy();

// Now create the multitenant container using the application
// container and the tenant identification strategy.
var mtc = new MultitenantContainer(tenantIdentifier, appContainer);

// Configure the overrides for each tenant by passing in the tenant ID
// and a lambda that takes a ContainerBuilder.
mtc.ConfigureTenant(
  '1',
  b => b.RegisterType<Tenant1Dependency>()
    .As<IDependency>()
    .InstancePerDependency());
mtc.ConfigureTenant(
  '2',
  b => b.RegisterType<Tenant2Dependency>()
    .As<IDependency>()
    .SingleInstance());

// Now you can use the multitenant container to resolve instances.
```

[Check out the documentation](https://autofac.readthedocs.io/en/latest/advanced/multitenant.html) for more usage details.

## Get Help

**Need help with Autofac?** We have [a documentation site](https://autofac.readthedocs.io/) as well as [API documentation](https://autofac.org/apidoc/). We're ready to answer your questions on [Stack Overflow](https://stackoverflow.com/questions/tagged/autofac) or check out the [discussion forum](https://groups.google.com/forum/#forum/autofac).
