# 🛡️ MXC-Sandbox: Microsoft eXecution Container for C# & AI Agents

[![.NET Version](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Security Posture](https://img.shields.io/badge/Security-Fail--Closed%20Deny--by--Default-success)](docs/csharp-mastery-course/01-mxc-architecture-and-fundamentals.md)
[![Agent Framework](https://img.shields.io/badge/Microsoft%20Agent%20Framework-MAF%20%2B%20MEAI-0078D4?logo=microsoft)](https://github.com/microsoft/agent-framework)

> **MXC (Microsoft eXecution Container)** is Microsoft's cross-platform sandboxed code execution system. This repository is the definitive **C# / .NET Developer Guide & Mastery Solution** for integrating MXC with **Microsoft Agent Framework (MAF)** to run LLM-generated code safely in production environments (Windows, Linux, macOS, Docker, Kubernetes).

---

## 📖 Table of Contents

- [Why Sandboxing Matters for AI Agents](#-why-sandboxing-matters-for-ai-agents)
- [Architecture Overview](#-architecture-overview)
- [Zero-to-Hero Mastery Course](#-zero-to-hero-mastery-course)
- [Quick Start](#-quick-start)
- [Microsoft Agent Framework (MAF) Integration](#-microsoft-agent-framework-maf-integration)
- [Cross-Platform Backends](#-cross-platform-backends)
- [Production Deployment (Docker & Kubernetes)](#-production-deployment-docker--kubernetes)
- [Security Invariants & Best Practices](#-security-invariants--best-practices)

---

## 🚨 Why Sandboxing Matters for AI Agents

Autonomous AI agents do not just answer questions—they write and run Python scripts, compile C# projects, execute Bash commands, and interact with the filesystem. Executing untrusted, model-generated code directly on host servers introduces critical risks:

1. **Privilege Escalation:** Malicious or hallucinated code modifying system binaries or accessing user profiles (`~/.aws`, `~/.ssh`).
2. **Credential Theft:** Exfiltrating host environment variables, tokens, and DPAPI secrets.
3. **SSRF & Lateral Movement:** Probing internal microservices, VPC endpoints, or cloud metadata (`169.254.169.254`).
4. **Denial of Service:** Runaway infinite loops, fork bombs, and memory saturation.

**MXC enforces a strict Fail-Closed, Deny-by-Default model:** If an isolation policy or resource limit cannot be strictly enforced by the host OS backend, MXC rejects the sandbox before launch.

---

## 🏛️ Architecture Overview

```
       +--------------------------------------------------------------+
       |             Microsoft Agent Framework (MAF) / .NET           |
       |       (DataScienceAgent, DevOpsCodingAgent, AITool Loops)    |
       +--------------------------------------------------------------+
                                      |
                           [Microsoft.Mxc.Sdk.V1]
                                      |
                      +---------------+---------------+
                      |   C# Managed SDK (V1 Layer)   |
                      |   - ContainerRequest / Policy  |
                      |   - MxcContainer (One-Shot)   |
                      |   - MxcLifecycle (Stateful)   |
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

## 🎓 Zero-to-Hero Mastery Course

A comprehensive 5-chapter course specifically authored for C# / .NET developers:

| Chapter | Title | Focus & Learning Goals |
| :--- | :--- | :--- |
| **[Chapter 1](docs/csharp-mastery-course/01-mxc-architecture-and-fundamentals.md)** | **MXC Architecture & Fundamentals** | Core mental model, isolation tiers, fail-closed security, and native OS backends. |
| **[Chapter 2](docs/csharp-mastery-course/02-csharp-sdk-in-depth.md)** | **The .NET C# SDK Deep Dive** | `MxcContainer` (Run/Spawn/PTY/Probe), `MxcLifecycle` stateful operations, filesystem & network policies. |
| **[Chapter 3](docs/csharp-mastery-course/03-microsoft-agent-framework-integration.md)** | **Microsoft Agent Framework Integration** | Building `MxcCodeInterpreterTool`, OmniRouter/Gemini Flash setup, and error self-correction loops. |
| **[Chapter 4](docs/csharp-mastery-course/04-cross-platform-deployments-docker-k8s-linux.md)** | **Cross-Platform Deployments** | Sandboxing inside Docker containers, Kubernetes Pods, user namespaces, and seccomp configurations. |
| **[Chapter 5](docs/csharp-mastery-course/05-production-hardening-security-and-best-practices.md)** | **Production Hardening & Threat Modeling** | Mitigating prompt injections, proxy egress mediation, concurrency governance (`SemaphoreSlim`), and auditing. |

---

## ⚡ Quick Start

### 1. Clone & Configure
```bash
# Copy template environment file
cp .env.example .env

# Edit .env with your OmniRouter or OpenAI-compatible endpoint
# (Defaults to http://localhost:20128/v1 and l-s-Poc/gemini-3.7-flash)
```

### 2. Run the C# Sample Application
```bash
cd samples/mxc-agentic-framework-csharp
dotnet run
```

---

## 🤖 Microsoft Agent Framework (MAF) Integration

Creating an autonomous sandboxed AI Agent in C# takes just a few lines of code:

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Mxc.Sdk.V1;
using MxcMafAgent.Tools;

// 1. Connect to OpenAI-compatible Gateway (OmniRoute, Azure OpenAI, vLLM)
IChatClient chatClient = new OpenAiCompatibleChatClient(
    baseUrl: "http://localhost:20128/v1",
    apiKey: "sk-your-api-key",
    modelId: "l-s-Poc/gemini-3.7-flash");

// 2. Instantiate MXC Sandbox Tool
var interpreter = new MxcCodeInterpreterTool();

// 3. Register Tool with MAF
AITool codeTool = AIFunctionFactory.Create(
    interpreter.ExecuteCodeAsync,
    name: "execute_sandboxed_code",
    description: "Executes Python, JS, C#, or Bash in an isolated MXC container.");

// 4. Run Agent with automatic self-repair loop
var agent = new DataScienceAgent(chatClient, interpreter);
string answer = await agent.RunAnalysisAsync("Calculate prime factors of 982451653");
Console.WriteLine(answer);
```

---

## 🌐 Cross-Platform Backends

| OS Platform | Backend | Isolation Mechanism |
| :--- | :--- | :--- |
| **Windows** | `ProcessContainer` | Windows AppContainer tokens, Job Objects, LowBox DACLs, Network SIDs |
| **Windows** | `IsolationSession` | Hyper-V hardware-enforced lightweight VM sandboxes |
| **Windows** | `WSLC` | Managed WSL2 guest daemon communicating over `AF_VSOCK` |
| **Linux** | `Bubblewrap` (`bwrap`) | Unprivileged user, mount, PID, and network namespaces (`slirp4netns` + `iptables-nft`) |
| **Linux** | `LXC` | Full system/application containers with `cgroups v2` |
| **macOS** | `Seatbelt` | Apple Sandbox Kernel Extension (`sandbox_init_with_parameters`) |
| **MicroVMs** | `NanVix` / `Hyperlight` | Microsecond-boot hypervisor sandboxes for extreme multi-tenant isolation |

---

## ☸️ Production Deployment (Docker & Kubernetes)

### Run in Docker
```bash
docker build -t mxc-sandbox-agent samples/mxc-agentic-framework-csharp/
docker run --rm -it --cap-add=SYS_ADMIN --cap-add=NET_ADMIN mxc-sandbox-agent
```

### Deploy to Kubernetes
```bash
kubectl apply -f samples/mxc-agentic-framework-csharp/k8s-deployment.yaml
```

---

## 🔒 Security Invariants & Best Practices

- **Zero Host Leakage:** Host environment variables are never inherited unless explicitly requested (`InheritDefaultEnvironment = false`).
- **RAM Disk Scratch Disks:** Use memory-backed tmpfs (`emptyDir: { medium: Memory }`) for lightning-fast, zero-trace execution.
- **Strict Directional Egress:** Network defaults to `deny`. Inter-process host loopback is blocked to prevent internal service probing.
- **Concurrency Pooling:** Guard execution throughput with `SemaphoreSlim` to prevent host CPU/memory starvation.

---

## 📄 License

Licensed under the [MIT License](LICENSE).
