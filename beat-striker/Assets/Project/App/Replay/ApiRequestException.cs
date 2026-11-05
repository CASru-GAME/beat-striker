using System;

namespace Alice {
    public class ApiRequestException : Exception {
        public long StatusCode { get; }
        public bool IsTransient => StatusCode == 0 || StatusCode == 408 || StatusCode == 429 || StatusCode >= 500 && StatusCode < 600;

        public ApiRequestException(long statusCode, string message, Exception innerException = null) : base(message, innerException) {
            StatusCode = statusCode;
        }
    }
}
