using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Opulence
{
    internal static class ScriptRunner
    {
        public static async Task<ApplicationWrapper?> RunCustomizationScriptAsync(OutputContext output, FileInfo projectFile)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            using (var step = output.BeginStep("Applying Application Customizations..."))
            {
                var scriptFilePath = DirectorySearch.AscendingSearch(projectFile.DirectoryName, "Opulence.csx");
                output.WriteDebugLine($"Looking for customization script above '{projectFile}'.");
                if (!File.Exists(scriptFilePath))
                {
                    output.WriteDebugLine($"No customization script found.");
                    step.MarkComplete("Skipping...");
                    return null;
                }

                output.WriteInfoLine($"Configuring application using '{Path.GetFileName(scriptFilePath)}'.");

                var code = File.ReadAllText(scriptFilePath);
                var script = CSharpScript.Create<object>(
                    code,
                    options: ScriptOptions.Default.WithImports(new[] { "System", "System.Collections.Generic", }),
                    globalsType: typeof(PipelineHolder),
                    assemblyLoader: null);
                script = script.ContinueWith<object>(@"return await Pipeline.ExecuteAsync(__Pipeline);", options: ScriptOptions.Default);

                output.WriteDebugLine($"Compiling {Path.GetFileName(scriptFilePath)}'.");
                script.Compile();
                var diagnostics = script.Compile();
                if (diagnostics.Length > 0)
                {
                    var builder = new StringBuilder();
                    output.WriteDebugLine($"Script '{scriptFilePath}' had compilation errors.");
                    builder.AppendLine($"Script '{scriptFilePath}' had compilation errors.");
                    foreach (var diagnostic in diagnostics)
                    {
                        output.WriteDebugLine(CSharpDiagnosticFormatter.Instance.Format(diagnostic));
                        builder.AppendLine(CSharpDiagnosticFormatter.Instance.Format(diagnostic));
                    }

                    throw new CommandException(builder.ToString());
                }
                output.WriteDebugLine($"Done compiling {Path.GetFileName(scriptFilePath)}'.");

                var pipeline = new CustomizationPipeline(
                    output,
                    rootDirectory: Path.GetDirectoryName(scriptFilePath)!,
                    name: Names.NormalizeToDns(Path.GetFileNameWithoutExtension(projectFile.Name)),
                    solution: null,
                    projectFile);
                var holder = new PipelineHolder(pipeline);

                output.WriteDebugLine($"Running {Path.GetFileName(scriptFilePath)}'.");
                object obj;
                try
                {
                    var result = await script.RunAsync(holder);
                    obj = result.ReturnValue;
                }
                catch (Exception ex)
                {
                    throw new CommandException($"Failed executing {Path.GetFileName(scriptFilePath)}'.", ex);
                }

                step.MarkComplete();
                return new ApplicationWrapper(obj, Path.GetDirectoryName(scriptFilePath)!);
            }
        }

        public static async Task<ApplicationWrapper?> RunCustomizationScriptAsync(OutputContext output, FileInfo solutionFile, SolutionFile solution)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (solutionFile is null)
            {
                throw new ArgumentNullException(nameof(solutionFile));
            }

            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            using (var step = output.BeginStep("Applying Application Customizations..."))
            {
                var scriptFilePath = Path.Combine(solutionFile.DirectoryName, "Opulence.csx");
                output.WriteDebugLine($"Looking for customization script at '{scriptFilePath}'.");
                if (!File.Exists(scriptFilePath))
                {
                    output.WriteDebugLine($"No customization script found.");
                    step.MarkComplete("Skipping...");
                    return null;
                }

                output.WriteInfoLine($"Configuring application using '{Path.GetFileName(scriptFilePath)}'.");

                var code = File.ReadAllText(scriptFilePath);
                var script = CSharpScript.Create<object>(
                    code,
                    options: ScriptOptions.Default.WithImports(new[] { "System", "System.Collections.Generic", }),
                    globalsType: typeof(PipelineHolder),
                    assemblyLoader: null);
                script = script.ContinueWith<object>(@"return await Pipeline.ExecuteAsync(__Pipeline);", options: ScriptOptions.Default);

                output.WriteDebugLine($"Compiling {Path.GetFileName(scriptFilePath)}'.");
                script.Compile();
                var diagnostics = script.Compile();
                if (diagnostics.Length > 0)
                {
                    var builder = new StringBuilder();
                    output.WriteDebugLine($"Script '{scriptFilePath}' had compilation errors.");
                    builder.AppendLine($"Script '{scriptFilePath}' had compilation errors.");
                    foreach (var diagnostic in diagnostics)
                    {
                        output.WriteDebugLine(CSharpDiagnosticFormatter.Instance.Format(diagnostic));
                        builder.AppendLine(CSharpDiagnosticFormatter.Instance.Format(diagnostic));
                    }

                    throw new CommandException(builder.ToString());
                }
                output.WriteDebugLine($"Done compiling {Path.GetFileName(scriptFilePath)}'.");

                var pipeline = new CustomizationPipeline(
                    output,
                    rootDirectory: Path.GetDirectoryName(scriptFilePath)!,
                    name: Names.NormalizeToDns(Path.GetFileNameWithoutExtension(solutionFile.Name)),
                    solution,
                    projectFile: null);
                var holder = new PipelineHolder(pipeline);

                output.WriteDebugLine($"Running {Path.GetFileName(scriptFilePath)}'.");
                object obj;
                try
                {
                    var result = await script.RunAsync(holder);
                    obj = result.ReturnValue;
                }
                catch (Exception ex)
                {
                    throw new CommandException($"Failed executing {Path.GetFileName(scriptFilePath)}'.", ex);
                }

                step.MarkComplete();
                return new ApplicationWrapper(obj, Path.GetDirectoryName(scriptFilePath)!);
            }
        }
    }
}
