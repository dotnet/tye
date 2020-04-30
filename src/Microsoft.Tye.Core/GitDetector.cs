// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public class GitDetector
    {
        public static GitDetector Instance { get; } = new GitDetector();

        private GitDetector()
        {
            IsGitInstalled = new Lazy<Task<bool>>(GetIsGitInstalled);
        }

        public Lazy<Task<bool>> IsGitInstalled { get; }

        private async Task<bool> GetIsGitInstalled()
        {
            try
            {
                var result = await ProcessUtil.RunAsync("git", "--version", throwOnError: false);
                return result.ExitCode == 0;
            }
            catch (Exception)
            {
                // Unfortunately, process throws
                return false;
            }
        }
    }
}
