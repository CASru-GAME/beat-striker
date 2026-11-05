using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Alice {
    internal static class ApiResponseParser {
        public static T Read<T>(UnityWebRequest request, T response, Func<T, bool> valid) where T : class {
            try {
                var json = request.downloadHandler.text;
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal)) {
                    throw new ArgumentException("The response must be a JSON object.");
                }
                JsonUtility.FromJsonOverwrite(json, response);
                if (!valid(response)) throw new ArgumentException("The response does not match the API contract.");
                return response;
            } catch (ArgumentException exception) {
                throw new ApiRequestException(request.responseCode, "API returned an invalid JSON response.", exception);
            }
        }
    }
}
