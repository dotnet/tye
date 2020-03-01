using System.Collections.Generic;
using Opulence;

namespace Tye
{
    internal class OpulenceApplicationAdapter : Opulence.Application
    {
        private readonly Micronetes.Hosting.Model.Application application;

        public OpulenceApplicationAdapter(
            Micronetes.Hosting.Model.Application application,
            ApplicationGlobals globals,
            IReadOnlyList<ServiceEntry> services)
        {
            this.application = application;
            Globals = globals;
            Services = services;
        }

        public override ApplicationGlobals Globals { get; }

        public override string RootDirectory => application.ContextDirectory;

        public override IReadOnlyList<ServiceEntry> Services { get; }
    }
}
