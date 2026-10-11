// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using MxcMafAgent.Tools;

namespace MxcMafAgent.Agents;

/// <summary>
/// Autonomous DevOps & Coding Agent that writes, compiles, tests, and repairs software in an MXC sandbox.
/// </summary>
public class DevOpsCodingAgent
{
    private readonly IChatClient _chatClient;
    private readonly MxcCodeInterpreterTool _codeInterpreter;

    public DevOpsCodingAgent(IChatClient chatClient, MxcCodeInterpreterTool codeInterpreter)
    {
        _chatClient = chatClient;
        _codeInterpreter = codeInterpreter;
    }

    /// <summary>
    /// Writes, tests, and validates code in an isolated sandbox.
    /// </summary>
    public async Task<string> BuildAndTestCodeAsync(string codingTask, CancellationToken cancellationToken = default)
    {
        AITool codeTool = AIFunctionFactory.Create(
            _codeInterpreter.ExecuteCodeAsync,
            name: "execute_sandboxed_code",
            description: "Runs code in an isolated MXC container. Returns compiler/interpreter stdout and stderr.");

        var chatOptions = new ChatOptions
        {
            Tools = [codeTool],
            Temperature = 0.0f
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are an Autonomous Software Engineering Agent.
                Your goal is to write robust, bug-free code that passes all unit tests.
                
                Workflow:
                1. Write your code and automated unit tests.
                2. Execute the code using the `execute_sandboxed_code` tool.
                3. If the build fails or unit tests fail, read the compiler errors/traceback from stderr.
                4. Refactor the code to fix the failure and re-run.
                5. Once all tests pass with ExitCode 0, output your final validated solution.
                """),
            new(ChatRole.User, codingTask)
        };

        int maxTurns = 6;
        for (int turn = 0; turn < maxTurns; turn++)
        {
            ChatResponse response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            messages.Add(new ChatMessage(ChatRole.Assistant, response.Message.Contents));

            var functionCalls = response.Message.Contents.OfType<FunctionCallContent>().ToList();
            if (!functionCalls.Any())
            {
                return response.Message.Text ?? "Task complete.";
            }

            foreach (var call in functionCalls)
            {
                if (call.Name == "execute_sandboxed_code" && call.Arguments is not null)
                {
                    call.Arguments.TryGetValue("language", out var langObj);
                    call.Arguments.TryGetValue("code", out var codeObj);

                    string lang = langObj?.ToString() ?? "python";
                    string code = codeObj?.ToString() ?? string.Empty;

                    Console.WriteLine($"\n[DevOps Agent Turn {turn + 1}]: Testing {lang} implementation...");

                    CodeExecutionResult result = await _codeInterpreter.ExecuteCodeAsync(
                        lang, 
                        code, 
                        cancellationToken: cancellationToken);

                    Console.WriteLine($"[Build Result]: ExitCode={result.ExitCode}, Success={result.Success}");
                    if (!result.Success && !string.IsNullOrEmpty(result.Stderr))
                    {
                        Console.WriteLine($"[Compiler/Runtime Error Caught]:\n{result.Stderr}");
                    }

                    messages.Add(new ChatMessage(ChatRole.Tool, [
                        new FunctionResultContent(call.CallId, result.ToString())
                    ]));
                }
            }
        }

        return "DevOps agent reached max turns without full completion.";
    }
}
