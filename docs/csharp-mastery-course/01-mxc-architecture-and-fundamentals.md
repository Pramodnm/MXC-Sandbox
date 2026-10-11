# Chapter 1: MXC Architecture and Fundamentals for .NET Developers

> **Target Audience:** C# / .NET Engineers, AI Platform Architects, Security Engineers  
> **Prerequisites:** Intermediate C#, understanding of process isolation concepts

---

## 1.1 The Sandboxing Problem in the Era of AI Agents

Autonomous AI Agents powered by Large Language Models (LLMs) do not simply answer text prompts; they generate and execute code, invoke CLI tools, manipulate filesystem artifacts, and query external APIs. Giving an unconstrained AI agent access to a raw host shell poses extreme security risks:

1. **Host Compromise & Privilege Escalation:** Untrusted code execution can modify host system binaries, read credentials (`~/.ssh`, `~/.aws`, Windows DPAPI secrets, environment variables), or establish reverse shells.
2. **Resource Exhaustion (Denial of Service):** Fork bombs, runaway infinite loops, or memory-hungry allocations can crash host servers.
3. **Lateral Network Movement & SSRF:** An agent could probe internal subnets, cloud metadata endpoints (`169.254.169.254`), or private databases.
4. **Data Exfiltration:** Malicious or hallucinated scripts can transmit proprietary data to arbitrary endpoints on the internet.

Traditional containerization (e.g., standard Docker, Podman) is often too heavy, requires root/privileged daemon access, introduces significant startup latency (~500ms–2s), and lacks fine-grained directional policy enforcement tailored for ephemeral agent tasks.

**MXC (Microsoft eXecution Container)** is Microsoft's cross-platform sandboxing and containment engine engineered specifically for secure, ephemeral, high-throughput code execution.

```
       +--------------------------------------------------------------+
       |             Microsoft Agent Framework (MAF) / .NET           |
       |             (AI Agents, Tools, Orchestrators, Loops)          |
       +--------------------------------------------------------------+
                                      |
                           [Microsoft.Mxc.Sdk.V1]
                                      |
                      +---------------+---------------+
                      |   C# Managed SDK (V1 Layer)   |
                      |   - ContainerRequest / Policy  |
                      |   - MxcContainer / Lifecycle  |
                      |   - MxcProcess / Streams / PTY|
                      +---------------+---------------+
                                      | (P/Invoke)
                      +---------------+---------------+
                      |            mxc_ffi            |
                      |    (C ABI Shared Library)     |
                      +---------------+---------------+
                                      |
                      +---------------+---------------+
                      |       mxc_engine (Rust)       |
                      |  - Backend Selection          |
                      |  - Fail-Closed Policy Check   |
                      |  - Normalization & Telemetry  |
                      +---------------+---------------+
                                      |
         +----------------------------+----------------------------+
         |                            |                            |
  [Windows Hosts]               [Linux Hosts]                [macOS / MicroVMs]
  - ProcessContainer (AppCont)  - Bubblewrap (bwrap)         - Seatbelt
  - IsolationSession (Hyper-V)  - LXC Containers             - NanVix MicroVM
  - WSLC (WSL2 daemon)          - Network namespaces         - Hyperlight
```

---

## 1.2 Core Architectural Invariants

MXC is built around rigorous engineering and security invariants:

### 1. Fail-Closed Security Policy
Every security restriction (filesystem access, directional networking, capability drops, UI isolation) **fails closed**. If an agent request specifies a policy or capability that the host backend cannot strictly enforce, MXC rejects the sandbox before launch. It never weakens validation or falls back to an unconstrained execution.

### 2. Deny-by-Default Execution Baseline
- **Filesystem:** The sandbox only sees dynamic linkers, standard system libraries, and standard tools. User directories (`$HOME`, `~/.aws`, `~/.ssh`), root partitions, and secret stores are completely invisible unless explicitly granted via `ReadonlyPaths` or `ReadwritePaths`.
- **Environment:** The host environment is never blindly inherited. The sandbox executes in a cleared environment (`--clearenv` / profile isolation) to eliminate accidental credential leakage.
- **Networking:** Ingress and Egress default to `deny`. Loopback communication with host services is strictly blocked unless explicitly authorized via peer-authenticated proxy endpoints.

### 3. Native Engine and Thin SDKs
The core containment validation, OS capability probing, and execution dispatch live in the compiled Rust engine (`mxc-sdk`, `mxc_engine`). The C# SDK (`Microsoft.Mxc.Sdk`) is a strongly-typed, memory-efficient, Native-AOT-compatible wrapper communicating via `mxc_ffi`.

---

## 1.3 Execution Surfaces: Transient vs. Persistent Lifecycle

MXC exposes two distinct execution models:

| Dimension | Transient Sandboxes (`MxcContainer`) | Persistent Lifecycle (`MxcLifecycle`) |
| :--- | :--- | :--- |
| **Mental Model** | "One-shot" / Create-and-Run | State-aware Container / Multi-Turn Session |
| **Typical Use Case** | Pure stateless scripts, unit tests, isolated evaluations | Interactive agent sessions, virtual environments, installing pip packages then running multiple scripts |
| **C# SDK Entrypoints** | `MxcContainer.RunAsync`, `MxcContainer.SpawnAsync`, `MxcContainer.SpawnWithPty` | `MxcLifecycle.ProvisionContainer`, `MxcLifecycle.StartContainer`, `MxcLifecycle.RunInContainerAsync`, `MxcLifecycle.StopContainer`, `MxcLifecycle.DeprovisionContainer` |
| **Container ID** | Implicit, managed automatically, destroyed on exit | Explicit opaque `ContainerId` returned on Provision |
| **State Retention** | Destroyed immediately upon process exit | Preserved across multiple executions until Deprovision |

---

## 1.4 Native Containment Backends

MXC automatically selects or allows explicit configuration of the optimal backend for the target platform:

### 1. Windows: ProcessContainer
- **Underlying Technology:** Windows AppContainer tokens, Job Objects, Restricted DACLs, LowBox tokens, Network isolation SIDs, and ETW Denial Capture.
- **Security Posture:** Isolates UI message loops, restricts clipboard read/write, limits window handles, and enforces fine-grained filesystem DACLs.
- **Probe Capability:** Supports `MxcContainer.Probe(request)` to dry-run AppContainer isolation tiers without spinning up a sandbox.

### 2. Windows & Virtualization: IsolationSession
- **Underlying Technology:** Hyper-V lightweight virtual machines / Windows host session sandboxes.
- **Security Posture:** Hardware-enforced VM boundary, dedicated ephemeral workspace, independent OS kernel.
- **Stateful Support:** Supports full `MxcLifecycle` stateful multi-turn execution and interactive PTY terminals.

### 3. Windows: WSLC (WSL2 Container)
- **Underlying Technology:** Managed WSL2 Linux guest daemon communicating over high-speed `AF_VSOCK`.
- **Security Posture:** Executes native Linux container requests seamlessly from a Windows host.

### 4. Linux: Bubblewrap (`bwrap`) — Default Linux Backend
- **Underlying Technology:** Unprivileged user namespaces (`CLONE_NEWUSER`), mount namespaces (`CLONE_NEWNS`), PID namespaces (`CLONE_NEWPID`), network namespaces (`CLONE_NEWNET`), and seccomp filters.
- **Rootless:** Does **not** require root privileges or a Docker daemon. Runs unprivileged inside modern Linux hosts and Kubernetes pods.
- **Networking:** Integrates with `slirp4netns` and in-namespace `iptables` (nftables backend) for strict directional packet filtering.

### 5. Linux: LXC
- **Underlying Technology:** Linux system and application containers utilizing `cgroups v2` and native Linux containment tooling for heavier persistent container workloads.

### 6. macOS: Seatbelt
- **Underlying Technology:** Apple Sandbox Kernel Extension (`sandbox_init_with_parameters` / `sandbox-exec`) using compiled Scheme sandbox profiles.
- **Security Posture:** Enforces `(deny default)` with explicit directory and network allows.

### 7. MicroVMs: NanVix & Hyperlight
- **Underlying Technology:** Ultra-lightweight hypervisor-based sandboxes (KVM, Hyper-V, macOS Hypervisor.framework) providing microsecond-level boot times with hardware VM guarantees for tenant-untrusted multi-tenant AI clouds.

---

## 1.5 The Anatomy of an MXC Request

Every sandbox execution is defined by four core dimensions in the .NET SDK:

```csharp
var request = new ContainerRequest("python analyze.py")
{
    // 1. Backend Selection
    Containment = new Containment.Process(),

    // 2. Filesystem Boundary
    Filesystem = new FilesystemPolicy
    {
        ReadonlyPaths = { "/app/data", "/usr/lib/python3.11" },
        ReadwritePaths = { "/tmp/agent_workspace" },
        DeniedPaths = { "/app/data/secrets.json" }
    },

    // 3. Directional Network Boundary
    Network = new NetworkPolicy
    {
        Egress = new NetworkEgressPolicy
        {
            Default = NetworkAction.Deny,
            Allow = 
            {
                new EgressRule
                {
                    To = { new NetworkTarget { Cidr = "140.82.112.0/20" } }, // GitHub
                    Ports = { new NetworkPort { Port = 443, Protocol = NetworkProtocol.Tcp } }
                }
            }
        },
        Ingress = new NetworkIngressPolicy
        {
            Default = NetworkAction.Deny,
            HostLoopback = NetworkAction.Deny
        }
    },

    // 4. Execution Controls & Timeout
    WorkingDirectory = "/tmp/agent_workspace",
    TimeoutMs = 15_000,
    InheritDefaultEnvironment = false,
    Environment = new Dictionary<string, string>
    {
        ["PYTHONUNBUFFERED"] = "1"
    }
};
```

---

## 1.6 Chapter Summary & Key Takeaways

1. **Security Invariant:** MXC never compromises on policy. An unsupported policy fails closed before any process starts.
2. **Multi-Platform Consistency:** Write your C# agent code once; MXC transparently translates policies to Windows AppContainers on Windows, Bubblewrap on Linux, and Seatbelt on macOS.
3. **Execution Choice:** Use `MxcContainer` for fast stateless script evaluation; use `MxcLifecycle` when agents need persistent workspace state across iterative turns.

In **Chapter 2**, we will dive deep into the C# .NET SDK API surface, safe handle management, streaming standard I/O, PTY terminals, and comprehensive error handling.
