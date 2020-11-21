// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace System.CommandLine
{
    internal static class ConsoleExtensions
    {
        private static bool? _isConsoleRedirectionCheckSupported;

        private static bool IsConsoleRedirectionCheckSupported
        {
            get
            {
                if (_isConsoleRedirectionCheckSupported != null)
                {
                    return _isConsoleRedirectionCheckSupported.Value;
                }

                try
                {
                    var check = Console.IsOutputRedirected;
                    _isConsoleRedirectionCheckSupported = true;
                }

                catch (PlatformNotSupportedException)
                {
                    _isConsoleRedirectionCheckSupported = false;
                }

                return _isConsoleRedirectionCheckSupported.Value;
            }
        }

        public static void SetTerminalForegroundColor(this IConsole console, ConsoleColor color)
        {
            if (console.GetType().GetInterfaces().Any(i => i.Name == "ITerminal"))
            {
                ((dynamic)console).ForegroundColor = color;
            }

            switch (IsConsoleRedirectionCheckSupported)
            {
                case true when !Console.IsOutputRedirected:
                case true:
                    Console.ForegroundColor = color;
                    break;
            }
        }

        public static void ResetTerminalForegroundColor(this IConsole console)
        {
            if (console.GetType().GetInterfaces().Any(i => i.Name == "ITerminal"))
            {
                ((dynamic)console).ForegroundColor = ConsoleColor.Red;
            }

            switch (IsConsoleRedirectionCheckSupported)
            {
                case true when !Console.IsOutputRedirected:
                case true:
                    Console.ResetColor();
                    break;
            }
        }
    }
}
