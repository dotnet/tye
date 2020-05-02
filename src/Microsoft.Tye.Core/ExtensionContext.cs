// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public sealed class ExtensionContext
    {
        public ExtensionContext(ApplicationBuilder application, HostOptions? options, OutputContext output, OperationKind operation)
        {
            Application = application;
            Options = options;
            Output = output;
            Operation = operation;
        }

        public ApplicationBuilder Application { get; }

        public HostOptions? Options { get; }

        public OutputContext Output { get; }

        public OperationKind Operation { get; }

        public enum OperationKind
        {
            LocalRun,
            Deploy,
        }
    }
}
