using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TemplateSync.Infrastructure;

namespace TemplateSync.Airtable
{
    public sealed class AirtableClientOptions
    {
        public Uri BaseAddress { get; set; } = new Uri("https://api.airtable.com/v0/");

        /// <summary>Total attempts per request, including the first. Bounded so errors can never loop.</summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>Airtable asks clients to wait 30 seconds after a rate-limit 429.</summary>
        public TimeSpan RateLimitWait { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan MaxJitter { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>First back-off for 5xx and network failures; doubles per attempt.</summary>
        public TimeSpan TransientBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        public string UserAgent { get; set; } = "DicomTemplateMaker";

        /// <summary>Optional sink for retry notices, e.g. a status label.</summary>
        public Action<string>? Notify { get; set; }
    }

    /// <summary>
    /// Minimal Airtable Web API client built on HttpClient. It deliberately exposes only
    /// single-request operations and handles errors with bounded, explicit rules:
    /// monthly-quota 429s fail immediately, rate-limit 429s wait 30 s and retry at most
    /// <see cref="AirtableClientOptions.MaxAttempts"/> times, and nothing ever retries forever.
    /// </summary>
    public sealed class AirtableHttpClient : IAirtableApi
    {
        private static readonly HttpMethod Patch = new HttpMethod("PATCH");

        private readonly HttpClient _http;
        private readonly string _token;
        private readonly IClock _clock;
        private readonly AirtableClientOptions _options;
        private readonly RequestThrottle _throttle;
        private readonly Random _random;

        public AirtableHttpClient(HttpClient http, string token, IClock? clock = null, AirtableClientOptions? options = null, RequestThrottle? throttle = null, Random? random = null)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("An Airtable personal access token is required.", nameof(token));
            }

            _http = http ?? throw new ArgumentNullException(nameof(http));
            _token = token.Trim();
            _clock = clock ?? SystemClock.Instance;
            _options = options ?? new AirtableClientOptions();
            _throttle = throttle ?? RequestThrottle.Shared;
            _random = random ?? new Random();
            if (_options.MaxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "MaxAttempts must be at least 1.");
            }
        }

        public async Task<ListRecordsPage> ListRecordsPageAsync(ListRecordsRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Uri uri = BuildListUri(request);
            return await SendAsync(
                request.BaseId,
                retryAmbiguousFailures: true,
                () => new HttpRequestMessage(HttpMethod.Get, uri),
                (body, response) =>
                {
                    var parsed = JsonConvert.DeserializeObject<ListResponse>(body, Json.Settings) ?? new ListResponse();
                    return new ListRecordsPage(parsed.Records ?? new List<AirtableRecord>(), parsed.Offset, response.Headers.Date);
                },
                cancellationToken).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<AirtableRecord>> CreateRecordsAsync(string baseId, string table, IReadOnlyList<JObject> records, bool typecast, CancellationToken cancellationToken)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            CheckBatchSize(records.Count);
            var payload = new JObject
            {
                ["records"] = new JArray(records.Select(fields => new JObject { ["fields"] = fields })),
                ["typecast"] = typecast,
            };
            // A create is not idempotent: if Airtable committed it but the reply was lost, resending would
            // duplicate the records. Only rate-limit 429s (never committed) are retried automatically.
            return SendWriteAsync(baseId, table, HttpMethod.Post, payload, retryAmbiguousFailures: false, cancellationToken);
        }

        public Task<IReadOnlyList<AirtableRecord>> UpdateRecordsAsync(string baseId, string table, IReadOnlyList<RecordUpdate> updates, bool typecast, CancellationToken cancellationToken)
        {
            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            CheckBatchSize(updates.Count);
            var payload = new JObject
            {
                ["records"] = new JArray(updates.Select(u => new JObject { ["id"] = u.Id, ["fields"] = u.Fields })),
                ["typecast"] = typecast,
            };
            // A PATCH sends absolute values, so repeating it is harmless.
            return SendWriteAsync(baseId, table, Patch, payload, retryAmbiguousFailures: true, cancellationToken);
        }

        internal Uri BuildListUri(ListRecordsRequest request)
        {
            var query = new List<string>
            {
                "pageSize=" + Math.Max(1, Math.Min(request.PageSize, AirtableLimits.MaxPageSize)).ToString(CultureInfo.InvariantCulture),
            };
            if (request.Fields != null)
            {
                foreach (string field in request.Fields)
                {
                    query.Add("fields%5B%5D=" + Uri.EscapeDataString(field));
                }
            }

            if (!string.IsNullOrEmpty(request.FilterByFormula))
            {
                query.Add("filterByFormula=" + Uri.EscapeDataString(request.FilterByFormula));
            }

            if (request.SortFields != null)
            {
                for (int i = 0; i < request.SortFields.Count; i++)
                {
                    string index = i.ToString(CultureInfo.InvariantCulture);
                    query.Add("sort%5B" + index + "%5D%5Bfield%5D=" + Uri.EscapeDataString(request.SortFields[i]));
                    query.Add("sort%5B" + index + "%5D%5Bdirection%5D=asc");
                }
            }

            if (!string.IsNullOrEmpty(request.View))
            {
                query.Add("view=" + Uri.EscapeDataString(request.View));
            }

            if (!string.IsNullOrEmpty(request.Offset))
            {
                query.Add("offset=" + Uri.EscapeDataString(request.Offset));
            }

            return new Uri(TableUri(request.BaseId, request.Table), "?" + string.Join("&", query));
        }

        private Uri TableUri(string baseId, string table)
        {
            return new Uri(_options.BaseAddress, Uri.EscapeDataString(baseId) + "/" + Uri.EscapeDataString(table));
        }

        private static void CheckBatchSize(int count)
        {
            if (count < 1 || count > AirtableLimits.MaxRecordsPerWrite)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Airtable accepts 1 to 10 records per create or update request.");
            }
        }

        private Task<IReadOnlyList<AirtableRecord>> SendWriteAsync(string baseId, string table, HttpMethod method, JObject payload, bool retryAmbiguousFailures, CancellationToken cancellationToken)
        {
            Uri uri = TableUri(baseId, table);
            string json = payload.ToString(Formatting.None);
            return SendAsync<IReadOnlyList<AirtableRecord>>(
                baseId,
                retryAmbiguousFailures,
                () => new HttpRequestMessage(method, uri) { Content = new StringContent(json, Encoding.UTF8, "application/json") },
                (body, _) =>
                {
                    var parsed = JsonConvert.DeserializeObject<ListResponse>(body, Json.Settings) ?? new ListResponse();
                    return parsed.Records ?? new List<AirtableRecord>();
                },
                cancellationToken);
        }

        private async Task<T> SendAsync<T>(string baseId, bool retryAmbiguousFailures, Func<HttpRequestMessage> createRequest, Func<string, HttpResponseMessage, T> parse, CancellationToken cancellationToken)
        {
            for (int attempt = 1; ; attempt++)
            {
                bool lastAttempt = attempt >= _options.MaxAttempts;
                bool lastAmbiguousAttempt = lastAttempt || !retryAmbiguousFailures;
                await _throttle.WaitAsync(baseId, _clock, cancellationToken).ConfigureAwait(false);

                HttpResponseMessage response;
                using (HttpRequestMessage request = createRequest())
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                    request.Headers.UserAgent.ParseAdd(_options.UserAgent);
                    try
                    {
                        response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (IsTransientTransportFailure(ex, cancellationToken))
                    {
                        if (lastAmbiguousAttempt)
                        {
                            throw new AirtableUnavailableException("Could not reach Airtable: " + ex.Message, null, ex);
                        }

                        await BackOffTransientAsync(attempt, "Could not reach Airtable", cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                }

                using (response)
                {
                    string body = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return parse(body, response);
                    }

                    ParseError(body, out string? errorType, out string? errorMessage);
                    int status = (int)response.StatusCode;
                    string detail = errorMessage ?? errorType ?? response.ReasonPhrase ?? status.ToString(CultureInfo.InvariantCulture);

                    if (status == 429)
                    {
                        if (IsBillingLimit(errorType, errorMessage) || IsBillingLimit(null, body))
                        {
                            throw new AirtableQuotaExceededException(
                                $"Airtable's monthly API call limit for the workspace that owns base {baseId} has been reached ({detail}). The limit resets on the first day of the month.",
                                errorType);
                        }

                        if (lastAttempt)
                        {
                            throw new AirtableRateLimitException($"Airtable is still rate-limiting base {baseId} after {attempt} attempts. Try again in a minute.");
                        }

                        TimeSpan wait = _options.RateLimitWait + Jitter();
                        _options.Notify?.Invoke($"Airtable rate limit reached; waiting {Math.Ceiling(wait.TotalSeconds)} s (attempt {attempt + 1} of {_options.MaxAttempts}).");
                        await _clock.Delay(wait, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (status == 401 || status == 403)
                    {
                        throw new AirtableAuthException(
                            $"Airtable rejected the access token for base {baseId} ({detail}). Check that the token is valid and has the data.records:read scope (and data.records:write to write) and access to this base.",
                            response.StatusCode,
                            errorType);
                    }

                    if (status == 404)
                    {
                        throw new AirtableNotFoundException($"Airtable could not find the base, table or record ({detail}). Check the base and table ids.", errorType);
                    }

                    if (status >= 500)
                    {
                        if (lastAmbiguousAttempt)
                        {
                            throw new AirtableUnavailableException($"Airtable returned HTTP {status} after {attempt} attempts ({detail}).", response.StatusCode, null);
                        }

                        await BackOffTransientAsync(attempt, $"Airtable returned HTTP {status}", cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new AirtableException($"Airtable rejected the request (HTTP {status}: {detail}).", response.StatusCode, errorType, null, errorMessage);
                }
            }
        }

        private static bool IsTransientTransportFailure(Exception ex, CancellationToken cancellationToken)
        {
            if (ex is HttpRequestException)
            {
                return true;
            }

            // HttpClient.Timeout surfaces as TaskCanceledException without our token being cancelled.
            return ex is TaskCanceledException && !cancellationToken.IsCancellationRequested;
        }

        private async Task BackOffTransientAsync(int attempt, string reason, CancellationToken cancellationToken)
        {
            double factor = Math.Pow(2, attempt - 1);
            TimeSpan wait = TimeSpan.FromTicks((long)(_options.TransientBaseDelay.Ticks * factor)) + Jitter();
            _options.Notify?.Invoke($"{reason}; retrying in {Math.Ceiling(wait.TotalSeconds)} s (attempt {attempt + 1} of {_options.MaxAttempts}).");
            await _clock.Delay(wait, cancellationToken).ConfigureAwait(false);
        }

        private TimeSpan Jitter()
        {
            lock (_random)
            {
                return TimeSpan.FromMilliseconds(_random.NextDouble() * _options.MaxJitter.TotalMilliseconds);
            }
        }

        internal static bool IsBillingLimit(string? errorType, string? errorMessage)
        {
            string text = (errorType ?? string.Empty) + " " + (errorMessage ?? string.Empty);
            return text.IndexOf("BILLING", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("monthly", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Airtable errors come as {"error":{"type":..,"message":..}} or {"error":"NOT_FOUND"}.</summary>
        internal static void ParseError(string body, out string? errorType, out string? errorMessage)
        {
            errorType = null;
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            try
            {
                JObject root = JObject.Parse(body);
                JToken? error = root["error"];
                if (error is JObject obj)
                {
                    errorType = (string?)obj["type"];
                    errorMessage = (string?)obj["message"];
                }
                else if (error != null && error.Type == JTokenType.String)
                {
                    errorType = (string?)error;
                    errorMessage = (string?)root["message"];
                }
                else if (root["errors"] is JArray errors && errors.First is JObject first)
                {
                    // Shape reported for some endpoints: {"errors":[{"error":"TYPE","message":"..."}]}
                    errorType = (string?)first["error"] ?? (string?)first["type"];
                    errorMessage = (string?)first["message"];
                }
            }
            catch (JsonException)
            {
                errorMessage = body.Length > 300 ? body.Substring(0, 300) : body;
            }
            catch (InvalidCastException)
            {
                errorMessage = body.Length > 300 ? body.Substring(0, 300) : body;
            }
        }

        private sealed class ListResponse
        {
            [JsonProperty("records")]
            public List<AirtableRecord>? Records { get; set; }

            [JsonProperty("offset")]
            public string? Offset { get; set; }
        }
    }
}
