using System.Net.Http.Headers;
using System.Text.Json;

namespace Nomi;

public sealed record Release(Version Version, Uri Page);

public static class Updates
{
    public const string Api = "https://api.github.com/repos/solelyian/nomi/releases/latest";
    private const int MaxBytes = 512 * 1024;

    public static Release? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;
            if (!root.TryGetProperty("tag_name", out var tag) || tag.GetString() is not { } name) return null;
            if (!Version.TryParse(name.TrimStart('v', 'V'), out var version)) return null;
            if (!root.TryGetProperty("html_url", out var url) || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var page)) return null;
            if (page.Scheme != Uri.UriSchemeHttps || page.Host != "github.com" || !page.AbsolutePath.StartsWith("/solelyian/nomi/", StringComparison.Ordinal)) return null;
            return new(Normalize(version), page);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));

    public static bool Newer(Release release, Version current) => release.Version > Normalize(current);

    public static bool Due(DateTimeOffset? last, DateTimeOffset now) => last is not { } checkedAt || checkedAt > now || now - checkedAt >= TimeSpan.FromDays(1);

    public static async Task<Release?> FetchAsync(HttpClient http, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Api);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Nomi", typeof(Updates).Assembly.GetName().Version?.ToString(3) ?? "0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) return null;
        return Parse(await Limited.ReadAsync(response, MaxBytes, token));
    }
}

public static class Limited
{
    public static async Task<string> ReadAsync(HttpResponseMessage response, int maxBytes, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > maxBytes) throw new InvalidDataException("too-large");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[16384];
        int read;
        while ((read = await stream.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + read > maxBytes) throw new InvalidDataException("too-large");
            buffer.Write(chunk, 0, read);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
