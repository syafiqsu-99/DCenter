using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;

namespace DCenter.Server.IntegrationTests;

// Records every call of a scenario as text and compares it with Snapshots/<name>.txt.
// Set UPDATE_SNAPSHOTS=1 to (re)write the golden file from the current code.
public sealed class Snapshot(HttpClient client, string name, FixedTime time)
{
    // Values that differ between runs for reasons unrelated to behavior: entity defaults that read the
    // machine clock, SQL rowversions, encrypted tokens and trace ids.
    private static readonly HashSet<string> Volatile = new(StringComparer.OrdinalIgnoreCase)
        { "createdAt", "occurredAt", "sinceAt", "oldestSinceAt", "rowVersion", "token", "traceId" };

    private readonly StringBuilder log = new();

    public HttpClient Client => client;

    public Task<JsonNode?> Get(string step, string url) => Send(step, new HttpRequestMessage(HttpMethod.Get, url));
    public Task<JsonNode?> Delete(string step, string url) => Send(step, new HttpRequestMessage(HttpMethod.Delete, url));
    public Task<JsonNode?> Post(string step, string url, object? body = null) => Send(step, Json(HttpMethod.Post, url, body));
    public Task<JsonNode?> Put(string step, string url, object? body) => Send(step, Json(HttpMethod.Put, url, body));

    public Task<JsonNode?> Upload(string step, string url, string fileName, string content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", fileName);
        return Send(step, new HttpRequestMessage(HttpMethod.Post, url) { Content = form });
    }

    public void Note(string text) => log.AppendLine($"# {text}");

    private static HttpRequestMessage Json(HttpMethod method, string url, object? body)
        => new(method, url) { Content = body is null ? null : JsonContent.Create(body) };

    private async Task<JsonNode?> Send(string step, HttpRequestMessage request)
    {
        // Each call happens a minute after the previous one, as it would for a real user.
        time.Now = time.Now.AddMinutes(1);
        using var response = await client.SendAsync(request);
        var type = response.Content.Headers.ContentType?.MediaType ?? "";
        var bytes = await response.Content.ReadAsByteArrayAsync();
        log.AppendLine($"### {step}");
        log.AppendLine($"{request.Method} {request.RequestUri} -> {(int)response.StatusCode}");

        JsonNode? json = null;
        if (type.Contains("json") && bytes.Length > 0)
        {
            json = JsonNode.Parse(bytes);
            log.AppendLine(Normalize(json?.DeepClone())?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "null");
        }
        else if (type.Contains("spreadsheetml"))
        {
            using var wb = new XLWorkbook(new MemoryStream(bytes));
            foreach (var cell in wb.Worksheet(1).CellsUsed()) log.AppendLine($"{cell.Address}: {cell.GetString()}");
        }
        else if (type == "application/pdf")
        {
            log.AppendLine(bytes.Length > 1000 && Encoding.ASCII.GetString(bytes, 0, 4) == "%PDF" ? "<pdf>" : "<bad pdf>");
        }
        else if (bytes.Length > 0)
        {
            log.AppendLine(Encoding.UTF8.GetString(bytes).TrimStart('﻿').Replace("\r\n", "\n").TrimEnd());
        }
        log.AppendLine();
        return json;
    }

    private static JsonNode? Normalize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                    obj[key] = Volatile.Contains(key) && value is not null ? JsonValue.Create("<volatile>") : Normalize(value);
                return obj;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++) array[i] = Normalize(array[i]?.DeepClone());
                return array;
            default:
                return node;
        }
    }

    public void Verify()
    {
        var text = log.ToString().Replace("\r\n", "\n");
        var dir = Path.Combine(SourceDirectory(), "Snapshots");
        var path = Path.Combine(dir, $"{name}.txt");
        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, text);
            return;
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n");
        if (expected == text) return;

        var actualPath = Path.Combine(dir, $"{name}.actual.txt");
        File.WriteAllText(actualPath, text);
        var expectedLines = expected.Split('\n');
        var actualLines = text.Split('\n');
        var first = Enumerable.Range(0, Math.Min(expectedLines.Length, actualLines.Length))
            .FirstOrDefault(i => expectedLines[i] != actualLines[i], Math.Min(expectedLines.Length, actualLines.Length));
        Assert.Fail($"Snapshot {name} differs at line {first + 1}.\nexpected: {expectedLines.ElementAtOrDefault(first)}\nactual:   {actualLines.ElementAtOrDefault(first)}\nFull output: {actualPath}");
    }

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "")
        => Path.GetDirectoryName(path)!;
}
