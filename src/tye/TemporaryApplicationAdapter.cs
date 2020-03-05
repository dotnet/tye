// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Tye.ConfigModel;

namespace Tye
{
    internal class TemporaryApplicationAdapter : Tye.Application
    {
        private readonly ConfigApplication application;

        public TemporaryApplicationAdapter(
            ConfigApplication application,
            ApplicationGlobals globals,
            IReadOnlyList<ServiceEntry> services)
        {
            this.application = application;
            Globals = globals;
            Services = services;
        }

        public override ApplicationGlobals Globals { get; }

        public override string RootDirectory => application.Source.DirectoryName;

        public override IReadOnlyList<ServiceEntry> Services { get; }
    }
}
