# Stage 1: Build .NET Project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore
COPY ["MxcMafAgent.csproj", "./"]
RUN dotnet restore "MxcMafAgent.csproj"

# Copy source and publish
COPY . .
RUN dotnet publish "MxcMafAgent.csproj" -c Release -o /app/publish

# Stage 2: Runtime Environment with Sandboxing Dependencies
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

# Install runtime dependencies for Bubblewrap and code execution
RUN apt-get update && apt-get install -y --no-install-recommends \
    bubblewrap \
    slirp4netns \
    util-linux \
    iptables \
    python3 \
    nodejs \
    bash \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Switch iptables to nftables
RUN update-alternatives --set iptables /usr/sbin/iptables-nft \
    && update-alternatives --set ip6tables /usr/sbin/ip6tables-nft

# Create dedicated unprivileged user
RUN useradd -m -u 1000 -s /bin/bash agentuser \
    && mkdir -p /tmp/mxc_agent_sandboxes \
    && chown -R agentuser:agentuser /tmp/mxc_agent_sandboxes

COPY --from=build /app/publish .

USER agentuser
ENV PYTHONUNBUFFERED=1

ENTRYPOINT ["dotnet", "MxcMafAgent.dll"]
