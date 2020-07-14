using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing;
using Xunit;

namespace Microsoft.Tye.UnitTests
{
    public class FuncFinderTests : LoggedTest 
    {
        [Theory]
        [InlineData("v2", "x64", "2.")]
        [InlineData("v3", "x64", "3.")]
        [InlineData("v2", "x86", "2.")]
        [InlineData("v3", "x86", "3.")]
        public async Task FuncInstallerReturnsRightPaths(string version, string arch, string expectedVersionPart)
        {
            var funcDownloader = new FuncDetector();
            var path = await funcDownloader.PathToFunc(version, arch, downloadPath: null, logger: LoggerFactory.CreateLogger("AzureFunctionTest"), default, dryRun: true);

            // Verifying this path is a bit tricky, version isn't easily associated.
            // Using partial matches
            Assert.Contains(FuncDetector.GetAzureFunctionDirectory(), path);
            Assert.Contains(expectedVersionPart, path);
        }

        [Fact]
        public async Task FuncInstallerWritesToDifferentDirectory()
        {
            using var tmp = TempDirectory.Create();
            var funcDownloader = new FuncDetector();
            var path = await funcDownloader.PathToFunc("v3", "x64", tmp.DirectoryPath, logger: LoggerFactory.CreateLogger("AzureFunctionTest"), default, dryRun: true);

            Assert.Contains(tmp.DirectoryPath, path);
        }

        [Fact]
        public async Task FuncInstallerThrowsForInvalidVersion()
        {
            var funcDownloader = new FuncDetector();
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await funcDownloader.PathToFunc("v3z", "x64", downloadPath: null, logger: LoggerFactory.CreateLogger("AzureFunctionTest"), default, dryRun: true));
        }

        [Fact]
        public async Task FuncInstallerLogsForV1SayingUnsupported()
        {
            var logger = LoggerFactory.CreateLogger("AzureFunctionTest");
            var funcDownloader = new FuncDetector();
            var path = await funcDownloader.PathToFunc("v1", "x64", downloadPath: null, logger: logger, default, dryRun: true);
            Assert.Contains(TestSink.Writes, context => context.Message.Contains("Functions V1 are unsupported and untested in Tye."));
        }

        [Fact]
        public async Task FuncInstallerThrowsForInvalidArch()
        {
            var funcDownloader = new FuncDetector();
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await funcDownloader.PathToFunc("v3", "x64a", downloadPath: null, logger: LoggerFactory.CreateLogger("AzureFunctionTest"), default, dryRun: true));
        }

    }
}
