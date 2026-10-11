// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using MxcMafAgent.Tools;

namespace MxcMafAgent.Agents;

/// <summary>
/// Autonomous Data Science Agent powered by Microsoft Agent Framework and MXC Sandbox.
/// </summary>
public class DataScienceAgent
{
    private readonly IChatClient _chatClient;
    private readonly MxcCodeInterpreterTool _codeInterpreter;

    public DataScienceAgent(IChatClient chatClient, MxcCodeInterpreterTool codeInterpreter)
    {
        _chatClient = chatClient;
        _codeInterpreter = codeInterpreter;
    }

    /// <summary>
    /// Executes an end-to-end data analysis goal by generating and running sandboxed code.
    /// </summary>
    public async Task<string> RunAnalysisAsync(string userGoal, CancellationToken cancellationToken = default)
    {
        // Define tool metadata for LLM function calling
        AITool codeTool = AIFunctionFactory.Create(
            _codeInterpreter.ExecuteCodeAsync,
            name: "execute_sandboxed_code",
            description: "Runs Python, JS, or Bash code in an isolated secure sandbox and returns standard output and error.");

        var chatOptions = new ChatOptions
        {
            Tools = [codeTool],
            Temperature = 0.0f
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are an expert Data Science and Mathematical Computing Agent.
                You solve complex analytical queries by generating executable Python code.
                
                Rules:
                1. Always execute code using the `execute_sandboxed_code` tool to compute accurate results.
                2. Never guess or hallucinate numeric results.
                3. If your code produces an error, inspect stderr, fix the issue, and execute again.
                4. Return the final answer clearly with the verified stdout.
                """),
            new(ChatRole.User, userGoal)
        };

        // Multi-turn tool execution loop
        int maxTurns = 5;
        for (int turn = 0; turn < maxTurns; turn++)
        {
            ChatResponse response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            messages.Add(new ChatMessage(ChatRole.Assistant, response.Message.Contents));

            // Check if the model requested a tool call
            var functionCalls = response.Message.Contents.OfType<FunctionCallContent>().ToList();
            if (!functionCalls.Any())
            {
                // No more tool calls; return final LLM response
                return response.Message.Text ?? "Analysis complete.";
            }

            // Execute each tool call requested by the model
            foreach (var call in functionCalls)
            {
                if (call.Name == "execute_sandboxed_code" && call.Arguments is not null)
                {
                    call.Arguments.TryGetValue("language", out var langObj);
                    call.Arguments.TryGetValue("code", out var codeObj);

                    string lang = langObj?.ToString() ?? "python";
                    string code = codeObj?.ToString() ?? string.Empty;

                    Console.WriteLine($"\n[Agent -> MXC]: Executing {lang} script:\n--------------------------------------------------");
                    Console.WriteLine(code);
                    Console.WriteLine("--------------------------------------------------");

                    CodeExecutionResult result = await _codeInterpreter.ExecuteCodeAsync(
                        lang, 
                        code, 
                        cancellationToken: cancellationToken);

                    Console.WriteLine($"[MXC -> Agent]: ExitCode: {result.ExitCode}, Stdout: {result.Stdout.Trim()}");

                    messages.Add(new ChatMessage(ChatRole.Tool, [
                        new FunctionResultContent(call.CallId, result.ToString())
                    ]));
                }
            }
        }

        return "Reached maximum execution turn limit.";
    }
}
