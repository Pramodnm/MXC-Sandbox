// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Microsoft.Mxc.Sdk;
using Microsoft.Mxc.Sdk.V1;
using MxcMafAgent;
using MxcMafAgent.Agents;
using MxcMafAgent.Tools;
using MxcMafAgent.Workflows;

// 1. Automatically load configuration from .env files
EnvLoader.Load();

string omniRouterBaseUrl = Environment.GetEnvironmentVariable("OMNIROUTER_BASE_URL")
    ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
    ?? "http://localhost:20128/v1";

string omniRouterApiKey = Environment.GetEnvironmentVariable("OMNIROUTER_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? "sk-your-omnirouter-api-key-here";

string modelId = Environment.GetEnvironmentVariable("SPECIALIST_MODEL")
    ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
    ?? "l-s-Poc/gemini-3.7-flash";

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==================================================================");
Console.WriteLine("  MXC & Microsoft Agent Framework (MAF) C# Mastery Solution");
Console.WriteLine("==================================================================");
Console.ResetColor();
Console.WriteLine($"[Config] Endpoint: {omniRouterBaseUrl}");
Console.WriteLine($"[Config] Model:    {modelId}");
Console.WriteLine($"[Config] API Key:  {omniRouterApiKey[..Math.Min(12, omniRouterApiKey.Length)]}...");

// ============================================================================
// 1. Host Platform & Containment Capabilities Discovery
// ============================================================================
Console.WriteLine("\n[1] Checking Host Containment Support...");
PlatformSupport support = MxcPlatform.GetPlatformSupport();
Console.WriteLine($"  Host Support:    {(support.IsSupported ? "SUPPORTED" : "UNSUPPORTED")}");
Console.WriteLine($"  Primary Backend: {support.Backend}");
if (!string.IsNullOrEmpty(support.Reason))
{
    Console.WriteLine($"  Reason:          {support.Reason}");
}

IReadOnlyList<AvailableBackend> backends = MxcPlatform.GetAvailableBackends();
Console.WriteLine($"\n  Available Native Backends ({backends.Count}):");
foreach (var b in backends)
{
    Console.WriteLine($"  - {b.Backend} (Tier: {b.Tier?.ToString() ?? "N/A"})");
    if (b.Capabilities.Any())
    {
        Console.WriteLine($"    Capabilities: {string.Join(", ", b.Capabilities)}");
    }
}

// ============================================================================
// 2. Direct Sandboxed Code Execution (One-Shot MxcContainer)
// ============================================================================
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n[2] Executing Direct Stateless Code in MXC Container...");
Console.ResetColor();

var interpreter = new MxcCodeInterpreterTool();

string pythonCode = """
import math

def compute_fibonacci(n):
    if n <= 0: return 0
    elif n == 1: return 1
    a, b = 0, 1
    for _ in range(2, n + 1):
        a, b = b, a + b
    return b

n = 50
fib_50 = compute_fibonacci(n)
print(f"Fibonacci({n}) = {fib_50}")
print(f"Number of digits: {len(str(fib_50))}")
""";

Console.WriteLine("Running Python Script in Sandbox...");
CodeExecutionResult pyResult = await interpreter.ExecuteCodeAsync("python", pythonCode);
Console.WriteLine(pyResult);

// ============================================================================
// 3. Security Boundary Enforcement Proof (Filesystem & Egress Isolation)
// ============================================================================
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n[3] Testing Security Boundary Enforcement...");
Console.ResetColor();

var tempWorkspace = Path.Combine(Path.GetTempPath(), "mxc_security_test_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempWorkspace);

try
{
    var secretFile = Path.Combine(tempWorkspace, "host_secret.txt");
    await File.WriteAllTextAsync(secretFile, "TOP_SECRET_CREDENTIALS");

    string testScript = OperatingSystem.IsWindows()
        ? $"cmd.exe /c \"type \"{secretFile}\" 2>nul || echo ACCESS_BLOCKED\""
        : $"sh -c \"cat '{secretFile}' 2>/dev/null || echo ACCESS_BLOCKED\"";

    var secureRequest = new ContainerRequest(testScript)
    {
        Containment = new Containment.Process(),
        TimeoutMs = 10_000,
        Filesystem = new FilesystemPolicy
        {
            ReadwritePaths = { Path.Combine(tempWorkspace, "sandbox_sub") }
        }
    };

    Directory.CreateDirectory(Path.Combine(tempWorkspace, "sandbox_sub"));

    ExecutionResult secResult = await MxcContainer.RunAsync(secureRequest);
    Console.WriteLine($"[Security Verification Output]: {secResult.Stdout.Trim()}");
}
finally
{
    try { Directory.Delete(tempWorkspace, recursive: true); } catch { }
}

// ============================================================================
// 4. Microsoft Agent Framework (MAF) Agent with OmniRoute & Gemini Flash
// ============================================================================
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n[4] Running Autonomous Data Science Agent with OmniRoute & Gemini Flash...");
Console.ResetColor();

using IChatClient chatClient = new OpenAiCompatibleChatClient(
    omniRouterBaseUrl,
    omniRouterApiKey,
    modelId);

var agent = new DataScienceAgent(chatClient, interpreter);

string prompt = "Calculate the prime factorization of 982451653 and verify that multiplying the factors gives the original number.";
Console.WriteLine($"User Goal: {prompt}");

string finalAnswer = await agent.RunAnalysisAsync(prompt);
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\n[Agent Final Verified Response]:\n{finalAnswer}");
Console.ResetColor();

Console.WriteLine("\n==================================================================");
Console.WriteLine("  All MXC & MAF Mastery scenarios completed successfully!");
Console.WriteLine("==================================================================");
