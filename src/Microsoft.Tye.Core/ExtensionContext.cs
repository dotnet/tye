// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public sealed class ExtensionContext
    {
        public ExtensionContext(ApplicationBuilder application, OutputContext output, OperationKind operation)
        {
            Application = application;
            Output = output;
            Operation = operation;
        }

        public ApplicationBuilder Application { get; }

        public OutputContext Output { get; }

        public OperationKind Operation { get; }

        public enum OperationKind
        {
            LocalRun,
            Deploy,
        }
    }
}
