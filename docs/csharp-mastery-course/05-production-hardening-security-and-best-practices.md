# Chapter 5: Production Hardening, Security Threat Modeling & Performance Best Practices

> **Target Audience:** Security Architects, Lead .NET Engineers, Platform SREs  
> **Prerequisites:** Chapters 1–4, Enterprise Security Architecture

---

## 5.1 Threat Modeling for Autonomous AI Agent Execution

When large language models generate and run code in real-time, the threat model must assume that the generated code is **adversarial by design** (e.g., through indirect prompt injection, poisoned context files, or compromised multi-agent communication).

```
   [Indirect Prompt Injection / Poisoned Data]
                       |
                       v
         [LLM Generates Malicious Payload]
                       |
                       v
  +-------------------------------------------------------------+
  |              MXC Multi-Layer Defense in Depth               |
  |                                                             |
  |  1. Cleared Environment Layer (--clearenv / no host env)   |
  |     -> Prevents secret extraction (AWS/Azure/API keys)      |
  |                                                             |
  |  2. Filesystem Jail Layer (Readonly baseline + tmpfs)       |
  |     -> Prevents host binary tampering & credential theft    |
  |                                                             |
  |  3. Directional Network Firewall (Private NetNS + Deny)     |
  |     -> Prevents reverse shells, C2 beacons, SSRF, & exfil   |
  |                                                             |
  |  4. Resource & Timeout Bounding (Job Objects / cgroups)     |
  |     -> Prevents DoS, memory saturation, and CPU lockups     |
  +-------------------------------------------------------------+
```

### Threat Vectors & MXC Mitigations

| Threat Vector | Attack Mechanism | MXC Defense & Mitigation |
| :--- | :--- | :--- |
| **Credential Theft** | Script reads `~/.aws/credentials`, `/etc/shadow`, or env vars | MXC clears the environment (`--clearenv`), masks `$HOME`, and binds a minimal read-only OS baseline. |
| **Data Exfiltration / SSRF** | Script sends stolen data via HTTP POST or connects to `169.254.169.254` | Directional egress defaults to `deny`. Sandboxes run in an isolated network namespace with loopback only. |
| **Host Binary Tampering** | Script attempts to overwrite `/usr/bin`, `/bin`, or `C:\Windows` | System directories are mounted strictly `read-only` or inaccessible under AppContainer DACLs. |
| **Symlink Traversal** | Script creates symlinks to escape scratch directory | MXC resolves canonical symlink paths prior to mounting and denies masked target destinations. |
| **Fork Bombs & Hangs** | Script runs `while(1) fork()` or `sleep(infinity)` | Strict `TimeoutMs` kills the process tree; OS Job Objects / cgroups constrain child processes. |

---

## 5.2 Hardening Directional Networking with Proxy Authentication

In enterprise deployments, agents often need to download standard packages (e.g., Python wheels from an internal PyPI mirror) but must never connect directly to the public internet or internal microservices.

MXC supports an **authenticated proxy pattern**:

```csharp
var networkPolicy = new NetworkPolicy
{
    // 1. Direct egress is blocked
    Egress = new NetworkEgressPolicy { Default = NetworkAction.Deny },

    // 2. Allow ingress from the local corporate proxy
    Ingress = new NetworkIngressPolicy
    {
        Default = NetworkAction.Allow,
        HostLoopback = NetworkAction.Deny // Block unauthenticated host loopback
    }
};

var request = new ContainerRequest("pip install pandas")
{
    Network = networkPolicy,
    Environment = new Dictionary<string, string>
    {
        // Force traffic through the corporate inspection proxy
        ["HTTP_PROXY"] = "http://10.0.0.15:8080",
        ["HTTPS_PROXY"] = "http://10.0.0.15:8080",
        ["NO_PROXY"] = "localhost,127.0.0.1"
    }
};
```

On Windows ProcessContainer backends, you can lock the proxy peer to an explicit Windows package identity SID (`AllowedProxyPeer = "Contoso.CorporateProxy_123"`), preventing any other host process from impersonating the proxy.

---

## 5.3 High-Throughput Concurrency & Worker Pooling

Launching thousands of concurrent agent requests requires managing host resource limits (PID limits, file descriptors, memory). 

### Thread-Safe Concurrency Governor Pattern

```csharp
public class SandboxedExecutionPool
{
    private readonly SemaphoreSlim _concurrencyGovernor;
    private readonly int _maxConcurrentSandboxes;

    public SandboxedExecutionPool(int maxConcurrentSandboxes = 16)
    {
        _maxConcurrentSandboxes = maxConcurrentSandboxes;
        _concurrencyGovernor = new SemaphoreSlim(maxConcurrentSandboxes, maxConcurrentSandboxes);
    }

    public async Task<ExecutionResult> ExecuteAsync(
        ContainerRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Wait for an available concurrency slot
        await _concurrencyGovernor.WaitAsync(cancellationToken);

        try
        {
            return await MxcContainer.RunAsync(request, cancellationToken: cancellationToken);
        }
        finally
        {
            _concurrencyGovernor.Release();
        }
    }
}
```

---

## 5.4 Monitoring & Auditing with Windows Denial Capture (`CaptureDenials`)

On Windows ProcessContainer backends, MXC provides **Denial Capture** via ETW (Event Tracing for Windows). When enabled, any ungranted access attempt by the sandboxed process (e.g. attempting to read a blocked registry key or folder) is captured into a structured diagnostic report:

```csharp
var request = new ContainerRequest("python untrusted_script.py")
{
    Containment = new Containment.ProcessContainer
    {
        CaptureDenials = new CaptureDenialsPolicy
        {
            Mode = CaptureDenialsMode.Block, // Block access and record event
            OutputPath = "C:\\logs\\sandbox_denials.json",
            RetainEtl = false
        }
    }
};

var result = await MxcContainer.RunAsync(request);

if (result.Metadata?.CaptureDenialsResult is { } denials)
{
    Console.WriteLine($"Total Denied Access Attempts: {denials.TotalDenials}");
    Console.WriteLine($"Denials Output File: {denials.OutputPath}");
}
```

---

## 5.5 Production Readiness Checklist

1. **Explicit Timeouts:** Always supply `TimeoutMs` on every `ContainerRequest` and `ExecutionRequest` (e.g. 15,000–30,000 ms).
2. **Deterministic Disposal:** Wrap `MxcProcess`, `MxcPtyProcess`, and `MxcLifecycle` sessions in `using` or `IAsyncDisposable` blocks to guarantee immediate resource release.
3. **RAM Disk Scratch Storage:** Point agent session working directories to memory-backed tmpfs (`emptyDir: { medium: Memory }` in Kubernetes) to maximize I/O throughput and ensure zero disk remnants on pod termination.
4. **Structured Logging:** Capture `result.ExitCode`, `result.TimedOut`, `result.Warnings`, and execution duration in your telemetry pipeline (OpenTelemetry, Application Insights).
5. **No Blind Environment Forwarding:** Set `InheritDefaultEnvironment = false` unless the child tool specifically requires host OS profile initialization. Never pass raw `Environment.GetEnvironmentVariables()` into `ContainerRequest.Environment`.

---

In the next section, we explore the **Complete Runnable C# Sample Project** implementing these concepts in Microsoft Agent Framework!
