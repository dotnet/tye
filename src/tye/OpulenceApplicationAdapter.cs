using System.Collections.Generic;
using Opulence;
using Tye.ConfigModel;

namespace Tye
{
    internal class OpulenceApplicationAdapter : Opulence.Application
    {
        private readonly ConfigApplication application;

        public OpulenceApplicationAdapter(
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
