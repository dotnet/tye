using System;
using System.Collections.Generic;
using System.Linq;

namespace Tye.Hosting
{
    public class ProcessRunnerOptions
    {
        public bool DebugMode { get; set; }
        public bool BuildProjects { get; set; }

        public static ProcessRunnerOptions FromArgs(string[] args)
        {
            return new ProcessRunnerOptions
            {
                BuildProjects = !args.Contains("--no-build"),
                DebugMode = args.Contains("--debug")
            };
        }
    }
}
