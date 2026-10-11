# Microsoft Agent Framework (MAF) + MXC C# Sample

This sample demonstrates how to build production-grade, secure AI Agents using **Microsoft Agent Framework (`Microsoft.Agents.AI` / `Microsoft.Extensions.AI`)** and **MXC (Microsoft eXecution Container)** in C# and .NET.

---

## 🌟 What This Sample Demonstrates

1. **`MxcCodeInterpreterTool`**: Production `AITool` / `AIFunction` executing Python, JavaScript, C#, and Bash in ephemeral, fail-closed sandboxes.
2. **`DataScienceAgent`**: Autonomous agent solving math, statistical, and data computing problems by generating and executing code.
3. **`DevOpsCodingAgent`**: Agent that compiles, tests, and repairs software bugs in isolated containers using compiler feedback.
4. **`StatefulSandboxSession`**: Multi-turn container lifecycle manager maintaining state across conversational turns.
5. **Security Isolation Verification**: Proves that sandboxed processes cannot read host secrets or access forbidden directories.

---

## 🚀 Running the Sample

### Prerequisites
- .NET 8.0 or .NET 9.0 SDK
- Python 3 / Node.js installed on your host (or run in Docker)
- Optional: `OPENAI_API_KEY` environment variable (if omitted, runs with built-in deterministic `MockChatClient` for offline verification).

### Run Locally via .NET CLI
```bash
cd samples/mxc-agentic-framework-csharp
dotnet run
```

---

## 🐳 Building & Running in Docker

```bash
docker build -t mxc-maf-agent .
docker run --rm -it --cap-add=SYS_ADMIN --cap-add=NET_ADMIN mxc-maf-agent
```

---

## ☸️ Deploying to Kubernetes

```bash
kubectl apply -f k8s-deployment.yaml
```

---

## 📚 Complete Course & Documentation

For in-depth architectural guides, API references, and deployment best practices, refer to the [C# Mastery Course](../../docs/csharp-mastery-course/INDEX.md):
- **[Chapter 1: MXC Architecture & Fundamentals](../../docs/csharp-mastery-course/01-mxc-architecture-and-fundamentals.md)**
- **[Chapter 2: .NET C# SDK Deep Dive](../../docs/csharp-mastery-course/02-csharp-sdk-in-depth.md)**
- **[Chapter 3: Microsoft Agent Framework Integration](../../docs/csharp-mastery-course/03-microsoft-agent-framework-integration.md)**
- **[Chapter 4: Cross-Platform Deployments (Docker, K8s, Linux, Windows)](../../docs/csharp-mastery-course/04-cross-platform-deployments-docker-k8s-linux.md)**
- **[Chapter 5: Production Hardening, Threat Modeling & Best Practices](../../docs/csharp-mastery-course/05-production-hardening-security-and-best-practices.md)**
