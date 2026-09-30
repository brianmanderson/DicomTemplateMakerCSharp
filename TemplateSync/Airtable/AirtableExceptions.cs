using System;
using System.Net;

namespace TemplateSync.Airtable
{
    /// <summary>Base type for every failure talking to Airtable. Messages are written for end users.</summary>
    public class AirtableException : Exception
    {
        public AirtableException(string message, HttpStatusCode? statusCode = null, string? errorType = null, Exception? inner = null, string? apiMessage = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
            ErrorType = errorType;
            ApiMessage = apiMessage;
        }

        /// <summary>The raw "message" member of Airtable's error body, when there was one.</summary>
        public string? ApiMessage { get; }

        public HttpStatusCode? StatusCode { get; }

        /// <summary>The "type" member of Airtable's error body, e.g. UNKNOWN_FIELD_NAME.</summary>
        public string? ErrorType { get; }
    }

    /// <summary>The workspace hit its monthly API call allowance (HTTP 429 PUBLIC_API_BILLING_LIMIT_EXCEEDED). Never retried.</summary>
    public sealed class AirtableQuotaExceededException : AirtableException
    {
        public AirtableQuotaExceededException(string message, string? errorType)
            : base(message, (HttpStatusCode)429, errorType)
        {
        }
    }

    /// <summary>Per-second rate limit still hit after the bounded retries.</summary>
    public sealed class AirtableRateLimitException : AirtableException
    {
        public AirtableRateLimitException(string message)
            : base(message, (HttpStatusCode)429, "RATE_LIMIT_REACHED")
        {
        }
    }

    /// <summary>Token missing, invalid, or lacking the scope/permission for this base (401/403).</summary>
    public sealed class AirtableAuthException : AirtableException
    {
        public AirtableAuthException(string message, HttpStatusCode statusCode, string? errorType)
            : base(message, statusCode, errorType)
        {
        }
    }

    /// <summary>Base or table id does not exist or is not visible to the token (404).</summary>
    public sealed class AirtableNotFoundException : AirtableException
    {
        public AirtableNotFoundException(string message, string? errorType)
            : base(message, HttpStatusCode.NotFound, errorType)
        {
        }
    }

    /// <summary>Airtable or the network was unavailable after the bounded retries.</summary>
    public sealed class AirtableUnavailableException : AirtableException
    {
        public AirtableUnavailableException(string message, HttpStatusCode? statusCode, Exception? inner)
            : base(message, statusCode, null, inner)
        {
        }
    }
}
