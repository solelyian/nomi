using System.Net;
using System.Text;
using System.Text.Json;
using Nomi;

const string Frames = "{\"message\":{\"content\":\"Résultat : 68 €\"},\"done\":false}\n{\"done\":true}";
var count = 0;
foreach (var action in new[] { "understand", "verify", "convert", "plan", "communicate", "learn" })
{
    foreach (var language in new[] { "fr", "en" })
    {
        var handler = new FakeHandler(async (request, _) =>
        {
            if (request.Content is null) throw new InvalidOperationException("Missing request body");
            using var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync());
            var root = body.RootElement;
            Assert(request.RequestUri?.IsLoopback == true, "Non-local request");
            Assert(root.GetProperty("model").GetString() == "qwen3:4b-instruct", "Incorrect model");
            Assert(!root.GetProperty("think").GetBoolean(), "Unexpected thinking mode");
            var messages = root.GetProperty("messages");
            Assert(messages[0].GetProperty("role").GetString() == "system", "Missing system prompt");
            Assert(messages[0].GetProperty("content").GetString()!.Contains("never generate, rewrite or modify code"), "Missing software guardrail");
            Assert(messages[1].GetProperty("content").GetString() == "80 € moins 15 %", "Prompt not preserved");
            return Response(Frames);
        });
        using var client = new OllamaClient(handler);
        var output = new StringBuilder();
        var truncated = await client.GenerateAsync(" 80 € moins 15 % ", action, language, "steps",
            token => output.Append(token), CancellationToken.None);
        Assert(output.ToString() == "Résultat : 68 €" && !truncated, "Streaming failed");
        count++;
    }
}

foreach (var (body, expected) in new[]
{
    ("{\"message\":{\"content\":\"Partial\"}}", "incomplete-response"),
    ("{\"done\":true}", "incomplete-response"),
    ("{\"error\":\"failed\"}", "engine-unavailable")
})
{
    using var client = new OllamaClient(new FakeHandler((_, _) => Task.FromResult(Response(body))));
    await ExpectError(() => client.GenerateAsync("test", "verify", "en", "summary", _ => { }, CancellationToken.None), expected);
    count++;
}
foreach (var (status, expected) in new[]
{
    (HttpStatusCode.NotFound, "model-missing"),
    (HttpStatusCode.TooManyRequests, "busy"),
    (HttpStatusCode.InternalServerError, "engine-unavailable")
})
{
    using var client = new OllamaClient(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
    await ExpectError(() => client.GenerateAsync("test", "verify", "en", "summary", _ => { }, CancellationToken.None), expected);
    count++;
}
using (var client = new OllamaClient(new FakeHandler((_, _) => Task.FromResult(Response(Frames)))))
{
    foreach (var prompt in new[] { " ", new string('a', 6001) })
    {
        await ExpectError(() => client.GenerateAsync(prompt, "verify", "en", "steps", _ => { }, CancellationToken.None), "invalid-request");
        count++;
    }
}
using (var client = new OllamaClient(new FakeHandler((_, _) => Task.FromResult(Response(Frames.Replace("\"done\":true", "\"done\":true,\"done_reason\":\"length\""))))))
{
    Assert(await client.GenerateAsync("test", "plan", "en", "summary", _ => { }, CancellationToken.None), "Truncation not reported");
    count++;
}
using (var client = new OllamaClient(new FakeHandler(async (_, token) =>
{
    await Task.Delay(Timeout.InfiniteTimeSpan, token);
    return Response(Frames);
})))
{
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
    try
    {
        await client.GenerateAsync("test", "plan", "fr", "steps", _ => { }, cancellation.Token);
        throw new InvalidOperationException("Cancellation not propagated");
    }
    catch (OperationCanceledException) { count++; }
}
Console.WriteLine($"{count} C# inference checks passed.");
await ResponsePolicyChecks.RunAsync(args);
await DocumentChecks.RunAsync();
await ModelChecks.RunAsync(args);

if (args.Contains("--live"))
{
    using var client = new OllamaClient();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    await client.GenerateAsync("I have 90 minutes, three tasks of 20 minutes each, and two breaks of 5 minutes. How much time remains?",
        "plan", "en", "summary", Console.Write, timeout.Token);
    Console.WriteLine();
}

static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK)
{
    Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson")
};

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task ExpectError(Func<Task<bool>> action, string code)
{
    try
    {
        await action();
        throw new InvalidOperationException($"Expected {code}");
    }
    catch (InferenceException exception) when (exception.Message == code) { }
}

sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => respond(request, cancellationToken);
}
