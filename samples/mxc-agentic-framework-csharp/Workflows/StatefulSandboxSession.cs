// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Mxc.Sdk;
using Microsoft.Mxc.Sdk.V1;
using MxcMafAgent.Tools;

namespace MxcMafAgent.Workflows;

/// <summary>
/// Manages a persistent stateful container session across multiple turns of an AI Agent conversation.
/// Allows the agent to install dependencies, generate files, compile code, and inspect outputs incrementally.
/// </summary>
public sealed class StatefulSandboxSession : IAsyncDisposable
{
    private readonly ContainerId _containerId;
    private bool _isStarted;
    private bool _isDisposed;

    private StatefulSandboxSession(ContainerId containerId)
    {
        _containerId = containerId;
    }

    /// <summary>
    /// Provisions and starts a new stateful container session.
    /// </summary>
    public static async Task<StatefulSandboxSession> CreateAsync(
        bool allowNetworkEgress = false, 
        CancellationToken cancellationToken = default)
    {
        var networkPolicy = new NetworkPolicy
        {
            Egress = new NetworkEgressPolicy
            {
                Default = allowNetworkEgress ? NetworkAction.Allow : NetworkAction.Deny
            },
            Ingress = new NetworkIngressPolicy
            {
                Default = NetworkAction.Deny,
                HostLoopback = NetworkAction.Deny
            }
        };

        var provisionRequest = new IsolationSessionProvisionRequest(networkPolicy);
        ProvisionResult provisionResult = MxcLifecycle.ProvisionContainer(provisionRequest);

        var session = new StatefulSandboxSession(provisionResult.ContainerId);
        
        // Start the container
        MxcLifecycle.StartContainer(provisionResult.ContainerId);
        session._isStarted = true;

        return session;
    }

    /// <summary>
    /// Executes a command in the running container session and preserves filesystem/environment state.
    /// </summary>
    public async Task<CodeExecutionResult> ExecuteAsync(
        string commandLine, 
        int timeoutMs = 30_000, 
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            var execRequest = new ExecutionRequest(commandLine)
            {
                TimeoutMs = (uint)timeoutMs
            };

            ExecutionResult result = await MxcLifecycle.RunInContainerAsync(
                _containerId, 
                execRequest, 
                cancellationToken: cancellationToken);

            return new CodeExecutionResult(
                Success: result.ExitCode == 0,
                ExitCode: result.ExitCode,
                Stdout: result.Stdout,
                Stderr: result.Stderr,
                ExecutionTimeMs: 0,
                ErrorMessage: result.TimedOut ? "Command timed out." : null);
        }
        catch (MxcException ex)
        {
            return new CodeExecutionResult(
                Success: false,
                ExitCode: (int)ex.Code,
                Stdout: string.Empty,
                Stderr: $"Stateful Container Error: {ex.Message}",
                ExecutionTimeMs: 0,
                ErrorMessage: ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_isStarted)
        {
            try
            {
                MxcLifecycle.StopContainer(_containerId);
            }
            catch
            {
                // Best-effort stop
            }
        }

        try
        {
            MxcLifecycle.DeprovisionContainer(_containerId);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
