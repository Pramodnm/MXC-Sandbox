# MXC & Microsoft Agent Framework (MAF) C# Mastery Course

Welcome to the comprehensive, zero-to-hero mastery course for **MXC (Microsoft eXecution Container)** and its integration with **Microsoft Agent Framework (MAF)** in C# and .NET!

---

## 📚 Course Modules & Chapters

| Chapter | Title | Focus & Key Concepts |
| :--- | :--- | :--- |
| **[Chapter 1](01-mxc-architecture-and-fundamentals.md)** | **MXC Architecture & Fundamentals** | • AI Agent Sandboxing Challenges<br>• Core Architectural Invariants & Fail-Closed Model<br>• Transient vs. Persistent Containment<br>• Native Backends (ProcessContainer, Bubblewrap, WSLC, IsolationSession, Nanvix) |
| **[Chapter 2](02-csharp-sdk-in-depth.md)** | **The .NET C# SDK Deep Dive** | • `Microsoft.Mxc.Sdk.V1` Architecture<br>• `MxcContainer` (Run, RunAsync, Spawn, SpawnWithPty, Probe)<br>• `MxcLifecycle` (State-aware Provision, Start, Exec, Stop, Deprovision)<br>• Filesystem, Directional Network & UI Policies<br>• Native Error Handling (`MxcException`, `ErrorCode`) |
| **[Chapter 3](03-microsoft-agent-framework-integration.md)** | **Microsoft Agent Framework (MAF) Integration** | • Agent-Tool Architecture with `Microsoft.Agents.AI`<br>• Building the `MxcCodeInterpreterTool`<br>• Multi-Language Sandbox Runner (Python, C#, JS, Bash, PowerShell)<br>• Agent Self-Correction & Stderr Diagnostic Feedback Loop<br>• Multi-turn Stateful Workflows |
| **[Chapter 4](04-cross-platform-deployments-docker-k8s-linux.md)** | **Cross-Platform Deployment (Docker, K8s, Linux, Windows)** | • Sandboxing inside Containers / Pods<br>• Linux Kernel & Unprivileged User Namespace Configuration<br>• Multi-Stage Dockerfile for .NET + MXC<br>• Production Kubernetes Manifests & Pod Security Contexts<br>• Windows Host Configuration & Service Accounts |
| **[Chapter 5](05-production-hardening-security-and-best-practices.md)** | **Production Hardening, Security & Best Practices** | • Threat Modeling & Adversarial Prompt Injections<br>• Authenticated Network Egress Filtering & Proxy Mediation<br>• High-Throughput Concurrency Governor (`SemaphoreSlim`)<br>• Denial Capture & Auditing (`CaptureDenials`)<br>• Production Readiness Checklist |

---

## 🚀 Practical C# Sample Application

A production-grade, complete sample project demonstrating Microsoft Agent Framework with MXC code interpretation is available under:

📁 **`samples/mxc-agentic-framework-csharp/`**
- **`Tools/MxcCodeInterpreterTool.cs`**: MAF `AIFunction` / `AITool` executing sandboxed code.
- **`Workflows/StatefulSandboxSession.cs`**: Stateful multi-turn container lifecycle manager.
- **`Agents/DataScienceAgent.cs`**: Data science and mathematical analysis agent.
- **`Agents/DevOpsCodingAgent.cs`**: Automated code generator, compiler, and tester.
- **`Program.cs`**: Interactive console application demonstrating multiple real-world scenarios.
- **`Dockerfile` & `k8s-deployment.yaml`**: Ready-to-deploy cloud-native manifests.

---

## 🎯 Learning Path

1. Read **Chapter 1** to build a rock-solid mental model of containerization and OS isolation invariants.
2. Follow **Chapter 2** to master the C# SDK API surface, safe handles, and streaming models.
3. Dive into **Chapter 3** to build AI agents using Microsoft Agent Framework that execute code safely.
4. Review **Chapter 4** to configure your Linux, Docker, or Kubernetes clusters for inner sandboxing.
5. Apply **Chapter 5** to harden your security posture and scale to millions of safe code executions in production!
