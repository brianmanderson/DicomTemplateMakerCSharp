using System.Diagnostics;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TemplateEvaluationTool
{
    /// <summary>
    /// Records every HTTP request that passes through it as one JSON line of <c>requests.jsonl</c> (plan P2.6): start
    /// time, method, URL, status code and elapsed milliseconds; the request body of a POST or PATCH; the full response
    /// body of a non-2xx response. No header value is ever written: the Authorization header shows as
    /// <c>"redacted"</c> (or <c>"none"</c>), and every secret handed to the constructor is replaced by
    /// <c>[redacted]</c> wherever it would appear, in case a response ever echoes it. Each retry of
    /// <see cref="TemplateSync.Airtable.AirtableHttpClient"/> is its own line, because the handler sits below it.
    /// </summary>
    internal sealed class LoggingHandler : DelegatingHandler
    {
        public const string FileName = "requests.jsonl";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string path;
        private readonly string[] secrets;
        private readonly object fileLock = new object();

        public LoggingHandler(string path, HttpMessageHandler innerHandler, IEnumerable<string>? secrets = null)
            : base(innerHandler)
        {
            this.path = Path.GetFullPath(path);
            this.secrets = (secrets ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.Ordinal).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            DateTimeOffset started = DateTimeOffset.UtcNow;
            string? requestBody = null;
            if ((request.Method == HttpMethod.Post || request.Method == HttpMethod.Patch) && request.Content != null)
            {
                await request.Content.LoadIntoBufferAsync().ConfigureAwait(false);
                requestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            var watch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Append(Line(started, request, null, watch, requestBody, null, ex.GetType().Name + ": " + ex.Message));
                throw;
            }

            string? responseBody = null;
            if (!response.IsSuccessStatusCode && response.Content != null)
            {
                await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            Append(Line(started, request, (int)response.StatusCode, watch, requestBody, responseBody, null));
            return response;
        }

        private static JObject Line(DateTimeOffset started, HttpRequestMessage request, int? status, Stopwatch watch, string? requestBody, string? responseBody, string? error)
        {
            var line = new JObject
            {
                ["startedUtc"] = started.ToString("O", CultureInfo.InvariantCulture),
                ["method"] = request.Method.Method,
                ["url"] = request.RequestUri?.ToString(),
                ["status"] = status,
                ["elapsedMs"] = watch.ElapsedMilliseconds,
                ["authorization"] = request.Headers.Authorization == null ? "none" : "redacted",
            };
            if (requestBody != null)
            {
                line["requestBody"] = requestBody;
            }

            if (responseBody != null)
            {
                line["responseBody"] = responseBody;
            }

            if (error != null)
            {
                line["error"] = error;
            }

            return line;
        }

        private void Append(JObject line)
        {
            string text = line.ToString(Formatting.None);
            foreach (string secret in secrets)
            {
                text = text.Replace(secret, "[redacted]", StringComparison.Ordinal);
            }

            lock (fileLock)
            {
                File.AppendAllText(path, text + "\n", Utf8NoBom);
            }
        }
    }
}
