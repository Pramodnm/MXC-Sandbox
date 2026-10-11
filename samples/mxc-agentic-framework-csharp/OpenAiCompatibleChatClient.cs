// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace MxcMafAgent;

/// <summary>
/// Production OpenAI-compatible IChatClient connecting to OmniRoute, Azure OpenAI, vLLM, or LiteLLM.
/// Supports chat completions, streaming, and tool/function calling.
/// </summary>
public class OpenAiCompatibleChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly Uri _endpoint;

    public OpenAiCompatibleChatClient(string baseUrl, string apiKey, string modelId)
    {
        _modelId = modelId;
        _endpoint = new Uri(baseUrl.TrimEnd('/') + "/chat/completions");

        _httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public ChatClientMetadata Metadata => new("OpenAiCompatibleChatClient", _endpoint, _modelId);

    public async Task<ChatResponse> GetResponseAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var requestPayload = new JsonObject
        {
            ["model"] = options?.ModelId ?? _modelId,
            ["temperature"] = options?.Temperature ?? 0.0f
        };

        if (options?.MaxOutputTokens is { } maxTokens)
        {
            requestPayload["max_tokens"] = maxTokens;
        }

        // Build messages array
        var messagesArray = new JsonArray();
        foreach (var msg in chatMessages)
        {
            var msgObj = new JsonObject
            {
                ["role"] = msg.Role.Value.ToLowerInvariant()
            };

            if (msg.Role == ChatRole.Tool)
            {
                var functionResult = msg.Contents.OfType<FunctionResultContent>().FirstOrDefault();
                if (functionResult != null)
                {
                    msgObj["tool_call_id"] = functionResult.CallId;
                    msgObj["content"] = functionResult.Result?.ToString() ?? "";
                }
            }
            else if (msg.Role == ChatRole.Assistant && msg.Contents.OfType<FunctionCallContent>().Any())
            {
                var toolCallsArray = new JsonArray();
                foreach (var toolCall in msg.Contents.OfType<FunctionCallContent>())
                {
                    var argsJson = JsonSerializer.Serialize(toolCall.Arguments);
                    toolCallsArray.Add(new JsonObject
                    {
                        ["id"] = toolCall.CallId,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = toolCall.Name,
                            ["arguments"] = argsJson
                        }
                    });
                }
                msgObj["tool_calls"] = toolCallsArray;
                msgObj["content"] = null;
            }
            else
            {
                msgObj["content"] = msg.Text ?? "";
            }

            messagesArray.Add(msgObj);
        }
        requestPayload["messages"] = messagesArray;

        // Build tools array if available
        if (options?.Tools is { Count: > 0 } tools)
        {
            var toolsArray = new JsonArray();
            foreach (var t in tools)
            {
                if (t is AIFunction func)
                {
                    var paramObj = JsonSerializer.SerializeToNode(func.JsonSchema) as JsonObject ?? new JsonObject();
                    toolsArray.Add(new JsonObject
                    {
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = func.Name,
                            ["description"] = func.Description,
                            ["parameters"] = paramObj
                        }
                    });
                }
            }
            requestPayload["tools"] = toolsArray;
        }

        var requestContent = new StringContent(
            requestPayload.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        using var httpResponse = await _httpClient.PostAsync(_endpoint, requestContent, cancellationToken);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"OpenAI-compatible endpoint returned status {(int)httpResponse.StatusCode}: {responseBody}");
        }

        var json = JsonNode.Parse(responseBody)?.AsObject()
            ?? throw new JsonException("Null response from AI endpoint.");

        var choices = json["choices"]?.AsArray();
        var firstChoice = choices?.FirstOrDefault()?.AsObject();
        var messageNode = firstChoice?["message"]?.AsObject();

        var assistantRole = ChatRole.Assistant;
        var contents = new List<AIContent>();

        string? textContent = messageNode?["content"]?.GetValue<string?>();
        if (!string.IsNullOrEmpty(textContent))
        {
            contents.Add(new TextContent(textContent));
        }

        if (messageNode?["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var call in toolCalls.OfType<JsonObject>())
            {
                string callId = call["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
                var fnObj = call["function"]?.AsObject();
                string fnName = fnObj?["name"]?.GetValue<string>() ?? "";
                string fnArgsRaw = fnObj?["arguments"]?.GetValue<string>() ?? "{}";

                Dictionary<string, object?>? argsDict = null;
                try
                {
                    argsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(fnArgsRaw);
                }
                catch
                {
                    argsDict = new Dictionary<string, object?>();
                }

                contents.Add(new FunctionCallContent(callId, fnName, argsDict));
            }
        }

        return new ChatResponse(new ChatMessage(assistantRole, contents));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(chatMessages, options, cancellationToken);
        yield return new ChatResponseUpdate
        {
            Role = response.Message.Role,
            Contents = response.Message.Contents,
            Text = response.Message.Text
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
