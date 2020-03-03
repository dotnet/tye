using System;
using System.Diagnostics;
using System.Threading;
/// <summary>
/// Startup hooks are pieces of code that will run before a users program main executes
/// See: https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/host-startup-hook.md
/// The type must be named StartupHook without any namespace, and should be internal.
/// </summary>
internal class StartupHook
{
    /// <summary>
    /// Startup hooks are pieces of code that will run before a users program main executes
    /// See: https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/host-startup-hook.md
    /// </summary>
    public static void Initialize()
    {
        Console.WriteLine("Waiting for debugger to attach...");

        while (!Debugger.IsAttached)
        {
            Thread.Sleep(1000);
        }
    }
}
