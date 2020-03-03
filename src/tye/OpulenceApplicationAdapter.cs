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
