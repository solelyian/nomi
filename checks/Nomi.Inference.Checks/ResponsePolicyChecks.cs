using System.Net;
using System.Text;
using System.Text.Json;
using Nomi;

internal static class ResponsePolicyChecks
{
    public static async Task RunAsync(string[] args)
    {
        var cases = JsonSerializer.Deserialize<PolicyCase[]>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "response-policy-cases.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var item in cases)
        {
            var result = ResponsePolicy.Apply(item.Source, item.Format);
            Assert(result.Text == item.Expected, item.Name);
            Assert(result.Changes.SequenceEqual(item.Changes), $"{item.Name}: changes");
            Assert(result.Reasons.SequenceEqual(item.Reasons), $"{item.Name}: reasons");
            Assert(result.Accepted == (item.Reasons.Length == 0), $"{item.Name}: acceptance");
            if (result.Accepted)
            {
                var again = ResponsePolicy.Apply(result.Text, item.Format);
                Assert(again.Text == result.Text && again.Changes.Length == 0, $"{item.Name}: idempotence");
            }
            using var client = new OllamaClient(new FakeHandler((_, _) =>
                Task.FromResult(Response(Frame(item.Source) + "{\"done\":true}\n"))));
            if (result.Accepted)
            {
                var generated = await client.GenerateNomiAsync("test", "understand", "fr", item.Format, CancellationToken.None);
                Assert(generated.Text == result.Text && generated.Accepted &&
                    generated.Changes.SequenceEqual(result.Changes), $"{item.Name}: integration");
            }
            else await ExpectError(() => client.GenerateNomiAsync("test", "verify", "en", item.Format, CancellationToken.None), "policy-blocked");
        }
        foreach (var action in new[] { "understand", "verify", "convert", "plan", "communicate", "learn" })
        {
            foreach (var language in new[] { "fr", "en" })
            {
                var raw = language == "fr" ? "Bravo champion ! 68 €." : "Well done, little buddy! €68.";
                using var client = new OllamaClient(new FakeHandler((_, _) =>
                    Task.FromResult(Response(Frame(raw) + "{\"done\":true}\n"))));
                var result = await client.GenerateNomiAsync("test", action, language, "summary", CancellationToken.None);
                Assert(result.Text == (language == "fr" ? "68 €." : "€68."), $"{action}/{language}");
                Assert(result.Changes.SequenceEqual(["professional-tone"]), "Missing policy details");
            }
        }
        foreach (var (body, expected) in new[]
        {
            (Frame("Partial") + "{\"done\":true,\"done_reason\":\"length\"}", "incomplete-response"),
            (Frame("Partial"), "incomplete-response"),
            (Frame(new string('x', ResponsePolicy.MaxCharacters + 1)), "policy-blocked")
        })
        {
            using var client = new OllamaClient(new FakeHandler((_, _) => Task.FromResult(Response(body))));
            await ExpectError(() => client.GenerateNomiAsync("test", "verify", "en", "summary", CancellationToken.None), expected);
        }
        using (var client = new OllamaClient(new FakeHandler((_, _) => Task.FromResult(Response(Frame("Partial") + "not-json")))))
        {
            try
            {
                await client.GenerateNomiAsync("test", "verify", "en", "summary", CancellationToken.None);
                throw new InvalidOperationException("Invalid JSON returned a response");
            }
            catch (JsonException) { }
        }
        await CheckGatedStream(false);
        await CheckGatedStream(true);
        await CheckRegeneration(true);
        await CheckRegeneration(false);
        await CheckRegenerationCancellation();
        Console.WriteLine($"{cases.Length} shared policy fixtures, 12 action/language paths and 6 stream/error checks passed in C#.");
        Console.WriteLine("3 regeneration checks passed in C#.");
        if (args.Contains("--policy-live"))
        {
            using var client = new OllamaClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var result = await client.GenerateNomiAsync("Un prix de 80 € avec une remise de 15 %. Quel est le prix final ?",
                "convert", "fr", "steps", timeout.Token);
            Assert(result.Accepted && result.Text.Contains("68"), "Live adapted result");
            Console.WriteLine($"Policy {result.Version}: {string.Join(", ", result.Changes)}\n{result.Text}");
        }
    }

    private static async Task CheckRegeneration(bool accept)
    {
        var calls = 0;
        using var client = new OllamaClient(new FakeHandler(async (request, token) =>
        {
            calls++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var messages = body.RootElement.GetProperty("messages");
            Assert(messages.GetArrayLength() == 2, "Rejected draft was sent back to the model");
            Assert(messages[1].GetProperty("content").GetString() == "80 € moins 15 %", "Original prompt changed");
            Assert(messages[0].GetProperty("content").GetString()!.Contains(ResponsePolicy.RegenerationInstruction) == (calls == 2),
                "Regeneration instruction is missing or premature");
            return Response(Frame(calls == 2 && accept ? "68 €" : "32 €\nLe prix final est 68 €.") + "{\"done\":true}\n");
        }));
        if (accept)
        {
            var result = await client.GenerateNomiAsync("80 € moins 15 %", "convert", "fr", "steps", CancellationToken.None);
            Assert(result.Accepted && result.Text == "68 €", "Wrong draft returned");
            Assert(result.Changes.SequenceEqual(["regenerated"]), "Regeneration was not disclosed");
        }
        else await ExpectError(() => client.GenerateNomiAsync("80 € moins 15 %", "convert", "fr", "steps", CancellationToken.None), "policy-blocked");
        Assert(calls == 2, "Regeneration must have exactly one extra attempt");
    }

    private static async Task CheckRegenerationCancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var client = new OllamaClient(new FakeHandler(async (_, token) =>
        {
            calls++;
            if (calls == 1) return Response(Frame("32 €\nLe prix final est 68 €.") + "{\"done\":true}\n");
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Response(Frame("68 €") + "{\"done\":true}\n");
        }));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pending = client.GenerateNomiAsync("80 € moins 15 %", "convert", "fr", "steps", cancellation.Token);
        await started.Task.WaitAsync(cancellation.Token);
        Assert(!pending.IsCompleted, "Rejected draft escaped while regenerating");
        cancellation.Cancel();
        try
        {
            await pending;
            throw new InvalidOperationException("Cancelled regeneration returned text");
        }
        catch (OperationCanceledException) { }
        Assert(calls == 2, "Unexpected extra regeneration");
    }

    private static async Task CheckGatedStream(bool cancel)
    {
        using var stream = new GatedStream(Frame("Le total est 68 €. Quel est votre dia"), Frame("gnostic ?") + "{\"done\":true}\n");
        using var client = new OllamaClient(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        })));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = client.GenerateNomiAsync("test", "convert", "fr", "summary", cancellation.Token);
        await stream.Started.Task.WaitAsync(cancellation.Token);
        Assert(!response.IsCompleted, "Returned a partial response");
        if (cancel)
        {
            cancellation.Cancel();
            try
            {
                await response;
                throw new InvalidOperationException("Cancelled generation returned text");
            }
            catch (OperationCanceledException) { }
        }
        else
        {
            stream.Release.SetResult();
            await ExpectError(() => response, "policy-blocked");
        }
    }

    private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson")
    };
    private static string Frame(string text) => JsonSerializer.Serialize(new { message = new { content = text } }) + "\n";
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static async Task ExpectError(Func<Task<PolicyResult>> action, string code)
    {
        try
        {
            await action();
            throw new InvalidOperationException($"Expected {code}");
        }
        catch (InferenceException exception) when (exception.Message == code) { }
    }
    private sealed record PolicyCase(string Name, string Source, string Format, string Expected, string[] Changes, string[] Reasons);

    private sealed class GatedStream(string first, string last) : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly MemoryStream head = new(Encoding.UTF8.GetBytes(first));
        private readonly MemoryStream tail = new(Encoding.UTF8.GetBytes(last));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (head.Position < head.Length)
            {
                var count = await head.ReadAsync(buffer, cancellationToken);
                Started.TrySetResult();
                return count;
            }
            await Release.Task.WaitAsync(cancellationToken);
            return await tail.ReadAsync(buffer, cancellationToken);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) { head.Dispose(); tail.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
