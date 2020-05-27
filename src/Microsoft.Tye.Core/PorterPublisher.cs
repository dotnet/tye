using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    public static class PorterPublisher
    {
        public static async Task PublishAsync(OutputContext output, YamlDocument document, ApplicationBuilder builder)
        {
            using var tempFile = TempFile.Create();

            {
                await using var stream = File.OpenWrite(tempFile.FilePath);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
                var yamlStream = new YamlStream(document);
                yamlStream.Save(writer, assignAnchors: false);
            }

            var res = await ProcessUtil.RunAsync("porter", $"publish -f {tempFile.FilePath}", throwOnError: false);
            if (res.ExitCode != 0)
            {
                output.WriteInfoLine(res.StandardError);
                output.WriteInfoLine(res.StandardOutput);
            }

        }
    }
}
