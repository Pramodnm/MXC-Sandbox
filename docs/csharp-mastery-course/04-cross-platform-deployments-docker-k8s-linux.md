# Chapter 4: Cross-Platform Deployments: Docker, Kubernetes, Linux & Windows

> **Target Audience:** DevOps Engineers, Cloud Platform Architects, SREs  
> **Prerequisites:** Chapters 1–3, Docker, Kubernetes manifests, Linux Namespaces

---

## 4.1 The Challenge of "Sandboxing inside Containers"

In modern cloud-native architectures, AI agent worker applications run inside **Docker containers** or **Kubernetes Pods**. When an agent worker invokes MXC to execute untrusted code, MXC creates an inner sandbox.

```
+-----------------------------------------------------------------------------------+
|                             Kubernetes Node (Host Linux Kernel)                   |
|                                                                                   |
|  +-----------------------------------------------------------------------------+  |
|  |                   AI Agent Worker Pod (Docker Container)                    |  |
|  |                   Running .NET 9 + Microsoft Agent Framework                |  |
|  |                                                                             |  |
|  |  +-----------------------------------------------------------------------+  |  |
|  |  |                   MXC Sandbox (Inner Bubblewrap Process)              |  |  |
|  |  |                   - Private User & Mount Namespaces                   |  |  |
|  |  |                   - Private Network Namespace (slirp4netns)           |  |  |
|  |  |                   - Cleared Host Environment                          |  |  |
|  |  |                   - Untrusted Python / Node / Bash Script             |  |  |
|  |  +-----------------------------------------------------------------------+  |  |
|  +-----------------------------------------------------------------------------+  |
+-----------------------------------------------------------------------------------+
```

To enable this nested isolation safely without requiring full root daemon access inside the pod, MXC relies on **Bubblewrap (`bwrap`)** and **unprivileged Linux namespaces**.

---

## 4.2 Linux Host & Kernel Prerequisites

Whether deploying on bare metal, AWS EC2, Azure VMs, or Kubernetes worker nodes, the underlying Linux kernel must satisfy:

1. **Kernel Version:** Linux 3.8+ (Linux 5.15+ or 6.x recommended).
2. **Unprivileged User Namespaces:** Must be enabled.
   ```bash
   # Verify unprivileged user namespaces
   cat /proc/sys/kernel/unprivileged_userns_clone
   # Should output: 1

   # If 0, enable permanently via sysctl:
   echo "kernel.unprivileged_userns_clone=1" | sudo tee /etc/sysctl.d/99-userns.conf
   sudo sysctl --system
   ```
3. **AppArmor / Seccomp Restrictions (Debian 12+, Ubuntu 23.10+):**
   Modern Ubuntu / Debian kernels restrict unprivileged user namespaces via AppArmor. Ensure your container runtime or host profile permits `bwrap` namespace creation:
   ```bash
   sudo sysctl -w kernel.apparmor_restrict_unprivileged_userns=0
   ```

---

## 4.3 Building a Production Docker Image for .NET + MXC

Here is the complete multi-stage `Dockerfile` to build and package a C# MAF + MXC application on Linux:

```dockerfile
# Stage 1: Build the .NET Application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["MxcMafAgent.csproj", "./"]
RUN dotnet restore "MxcMafAgent.csproj"

# Copy source code and publish
COPY . .
RUN dotnet publish "MxcMafAgent.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Container with Sandboxing Dependencies
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install MXC runtime dependencies and target script runtimes
RUN apt-get update && apt-get install -y --no-install-recommends \
    bubblewrap \
    slirp4netns \
    util-linux \
    iptables \
    iproute2 \
    python3 \
    python3-pip \
    nodejs \
    npm \
    bash \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Ensure iptables points to nftables backend (required by MXC)
RUN update-alternatives --set iptables /usr/sbin/iptables-nft \
    && update-alternatives --set ip6tables /usr/sbin/ip6tables-nft

# Create an unprivileged user for the agent worker
RUN useradd -m -u 1000 -s /bin/bash agentuser \
    && mkdir -p /tmp/mxc_agent_sandboxes \
    && chown -R agentuser:agentuser /tmp/mxc_agent_sandboxes

COPY --from=build /app/publish .

# Run as unprivileged user
USER agentuser
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV PYTHONUNBUFFERED=1

ENTRYPOINT ["dotnet", "MxcMafAgent.dll"]
```

---

## 4.4 Kubernetes Deployment Configuration

When deploying the agent container to Kubernetes, the pod must have permission to create user and mount namespaces.

### `k8s-deployment.yaml`

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: mxc-agent-worker
  namespace: ai-workloads
  labels:
    app: mxc-agent-worker
spec:
  replicas: 3
  selector:
    matchLabels:
      app: mxc-agent-worker
  template:
    metadata:
      labels:
        app: mxc-agent-worker
    spec:
      containers:
      - name: agent-worker
        image: your-registry.azurecr.io/mxc-maf-agent:latest
        imagePullPolicy: Always
        
        # Security Context for Inner Sandboxing
        securityContext:
          allowPrivilegeEscalation: true
          capabilities:
            add:
              # Needed for namespace creation and in-namespace iptables rules
              - SYS_ADMIN
              - NET_ADMIN
          runAsNonRoot: true
          runAsUser: 1000
          seccompProfile:
            type: Unconfined # or custom seccomp profile allowing clone3/unshare/mount
            
        resources:
          requests:
            cpu: "1000m"
            memory: "2Gi"
          limits:
            cpu: "4000m"
            memory: "8Gi"
            
        env:
        - name: OPENAI_API_KEY
          valueFrom:
            secretKeyRef:
              name: ai-secrets
              key: openai-api-key
        - name: AZURE_OPENAI_ENDPOINT
          value: "https://my-openai-instance.openai.azure.com/"

        # Ephemeral scratch volume for isolated sandbox execution directories
        volumeMounts:
        - name: sandbox-storage
          mountPath: /tmp/mxc_agent_sandboxes

      volumes:
      - name: sandbox-storage
        emptyDir:
          medium: Memory # Use tmpfs RAM disk for blazing-fast sandbox I/O
          sizeLimit: 1Gi
```

---

## 4.5 Windows Environments (AppContainers & IsolationSessions)

When running on **Windows Server 2022 / Windows 11 / Azure Windows VMs**:

### 1. ProcessContainer (AppContainer)
- **Zero Configuration:** Works out-of-the-box on standard Windows installations.
- **Service Account Permissions:** If your .NET service runs under `NT AUTHORITY\NETWORK SERVICE` or a dedicated Managed Service Account (gMSA), ensure the service has `SeCreateSymbolicLinkPrivilege` and can create LowBox AppContainer tokens.

### 2. WSLC (WSL2 Backend)
- Ideal when a Windows host needs to execute Linux code (e.g. bash, Linux-only python wheels).
- Ensure WSL2 is installed: `wsl --install --no-distribution`.
- The WSLC daemon (`wxc-wslc-daemon`) runs in the background and bridges execution over `AF_VSOCK`.

### 3. Hyper-V IsolationSessions
- For multi-tenant hosting where software-level sandboxes are insufficient.
- Requires Hyper-V hypervisor enabled on the Windows host.
- Offers hardware-level VM boundary per container.

---

## 4.6 Verification Checklist Before Going to Production

| Checkpoint | Target State | Command / Verification |
| :--- | :--- | :--- |
| **Platform Support Probe** | `support.IsSupported == true` | Run `MxcPlatform.GetPlatformSupport()` on startup |
| **Bubblewrap Version** | `>= 0.5.0` | `bwrap --version` |
| **slirp4netns** | Installed & on PATH | `which slirp4netns` |
| **iptables Backend** | `iptables-nft` | `iptables -V` (must show `nf_tables`) |
| **User Namespaces** | Enabled | `cat /proc/sys/kernel/unprivileged_userns_clone` -> `1` |
| **Scratch Disk** | Fast tmpfs mount | Mount `/tmp/mxc_agent_sandboxes` as `emptyDir: { medium: Memory }` |

In **Chapter 5**, we will cover **Production Hardening, Security Threat Modeling, and Performance Optimization**.
