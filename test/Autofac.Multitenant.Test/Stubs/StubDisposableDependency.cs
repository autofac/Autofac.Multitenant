// Copyright (c) Autofac Project. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Autofac.Multitenant.Test.Stubs;

public sealed class StubDisposableDependency : IDisposable
{
    /* Intentionally a simple (and incorrect) disposable implementation.
     * We need it for testing if Dispose was called, not actually to do
     * the standard Dispose cleanup. */

    public bool IsDisposed { get; set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
