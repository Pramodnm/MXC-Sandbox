// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Mxc.Sdk;
using Microsoft.Mxc.Sdk.V1;

namespace MxcMafAgent.Tools;

/// <summary>
/// Structured result returned to the AI Agent describing code execution outcome.
/// </summary>
public record CodeExecutionResult(
    bool Success,
    int ExitCode,
    string Stdout,
    string Stderr,
    double ExecutionTimeMs,
    string? ErrorMessage = null)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Status]: {(Success ? "SUCCESS" : "FAILED")} (ExitCode: {ExitCode}, Duration: {ExecutionTimeMs:F1}ms)");
        if (!string.IsNullOrWhiteSpace(Stdout))
        {
            sb.AppendLine("[Standard Output]:");
            sb.AppendLine(Stdout.TrimEnd());
        }
        if (!string.IsNullOrWhiteSpace(Stderr))
        {
            sb.AppendLine("[Standard Error]:");
            sb.AppendLine(Stderr.TrimEnd());
        }
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            sb.AppendLine($"[Error]: {ErrorMessage}");
        }
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Production MAF Tool providing secure sandboxed code execution via MXC.
/// </summary>
public class MxcCodeInterpreterTool
{
    private readonly string _workspaceBase;

    public MxcCodeInterpreterTool(string? workspaceBase = null)
    {
        _workspaceBase = workspaceBase ?? Path.Combine(Path.GetTempPath(), "mxc_agent_sandboxes");
        Directory.CreateDirectory(_workspaceBase);
    }

    [Description("Executes code in an isolated, secure MXC container and returns stdout, stderr, and exit code.")]
    public async Task<CodeExecutionResult> ExecuteCodeAsync(
        [Description("Programming language to execute: 'python', 'javascript', 'csharp', 'bash', 'powershell'")]
        string language,
        [Description("The source code to execute.")]
        string code,
        [Description("Maximum execution timeout in milliseconds. Defaults to 20000 (20 seconds).")]
        int timeoutMs = 20_000,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionDir = Path.Combine(_workspaceBase, sessionId);
        Directory.CreateDirectory(sessionDir);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            string scriptFileName;
            string commandLine;

            switch (language.ToLowerInvariant())
            {
                case "python":
                case "py":
                    scriptFileName = "script.py";
                    commandLine = OperatingSystem.IsWindows()
                        ? $"python.exe \"{Path.Combine(sessionDir, scriptFileName)}\""
                        : $"python3 \"{Path.Combine(sessionDir, scriptFileName)}\"";
                    break;

                case "javascript":
                case "js":
                case "node":
                    scriptFileName = "index.js";
                    commandLine = $"node \"{Path.Combine(sessionDir, scriptFileName)}\"" ;
                    break;

                case "bash":
                case "sh":
                    scriptFileName = "script.sh";
                    commandLine = $"bash \"{Path.Combine(sessionDir, scriptFileName)}\"";
                    break;

                case "powershell":
                case "ps1":
                    scriptFileName = "script.ps1";
                    commandLine = $"powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{Path.Combine(sessionDir, scriptFileName)}\"";
                    break;

                case "csharp":
                case "cs":
                case "dotnet":
                    scriptFileName = "Program.cs";
                    commandLine = OperatingSystem.IsWindows()
                        ? $"dotnet.exe run --project \"{sessionDir}\""
                        : $"dotnet run --project \"{sessionDir}\"";
                    
                    // Create minimal C# project file for single-file execution
                    var csprojContent = """
                        <Project Sdk="Microsoft.NET.Sdk">
                          <PropertyGroup>
                            <OutputType>Exe</OutputType>
                            <TargetFramework>net8.0</TargetFramework>
                            <ImplicitUsings>enable</ImplicitUsings>
                            <Nullable>enable</Nullable>
                          </PropertyGroup>
                        </Project>
                        """;
                    await File.WriteAllTextAsync(Path.Combine(sessionDir, "SandboxApp.csproj"), csprojContent, cancellationToken);
                    break;

                default:
                    return new CodeExecutionResult(
                        Success: false,
                        ExitCode: -1,
                        Stdout: string.Empty,
                        Stderr: string.Empty,
                        ExecutionTimeMs: 0,
                        ErrorMessage: $"Unsupported language '{language}'. Supported languages: python, javascript, csharp, bash, powershell.");
            }

            var scriptFilePath = Path.Combine(sessionDir, scriptFileName);
            await File.WriteAllTextAsync(scriptFilePath, code, Encoding.UTF8, cancellationToken);

            var request = new ContainerRequest(commandLine)
            {
                Containment = new Containment.Process(),
                WorkingDirectory = sessionDir,
                TimeoutMs = (uint)timeoutMs,
                Filesystem = new FilesystemPolicy
                {
                    ReadwritePaths = { sessionDir }
                },
                Network = new NetworkPolicy
                {
                    Egress = new NetworkEgressPolicy { Default = NetworkAction.Deny },
                    Ingress = new NetworkIngressPolicy
                    {
                        Default = NetworkAction.Deny,
                        HostLoopback = NetworkAction.Deny
                    }
                },
                Environment = new Dictionary<string, string>
                {
                    ["PYTHONUNBUFFERED"] = "1",
                    ["NODE_ENV"] = "production",
                    ["DOTNET_NOLOGO"] = "1",
                    ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
                },
                InheritDefaultEnvironment = true
            };

            ExecutionResult result = await MxcContainer.RunAsync(request, cancellationToken: cancellationToken);
            stopwatch.Stop();

            if (result.TimedOut)
            {
                return new CodeExecutionResult(
                    Success: false,
                    ExitCode: 124,
                    Stdout: result.Stdout,
                    Stderr: result.Stderr + "\n[ERROR]: Code execution exceeded timeout deadline.",
                    ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                    ErrorMessage: "Execution timed out.");
            }

            return new CodeExecutionResult(
                Success: result.ExitCode == 0,
                ExitCode: result.ExitCode,
                Stdout: result.Stdout,
                Stderr: result.Stderr,
                ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (MxcException ex)
        {
            stopwatch.Stop();
            return new CodeExecutionResult(
                Success: false,
                ExitCode: (int)ex.Code,
                Stdout: string.Empty,
                Stderr: $"MXC Containment Policy Error: {ex.Message}",
                ExecutionTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                ErrorMessage: ex.Message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(sessionDir))
                {
                    Directory.Delete(sessionDir, recursive: true);
                }
            }
            catch
            {
                // Ephemeral disk cleanup
            }
        }
    }
}
