using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Alice {
    public interface IPlayerRecordsApiClient {
        Task<PlayerProfile> GetProfileAsync(PlayerProfile profile);
        Task<PlayerProfilePage> GetRankingAsync(int grade, string cursor);
        Task<BattleHistoryListResponse> GetHistoryAsync(string name, string cursor);
    }

    public class PlayerRecordsApiClient : IPlayerRecordsApiClient {
        readonly IAppNetworkSetting setting;
        public PlayerRecordsApiClient(IAppNetworkSetting setting) { this.setting = setting; }
        public Task<PlayerProfile> GetProfileAsync(PlayerProfile profile) => Request("/players", "", new PlayerProfile(), response => ValidProfile(response) && response.name == profile.name, profile);
        public Task<PlayerProfilePage> GetRankingAsync(int grade, string cursor) => Request("/players", $"&grade={grade}&cursor={UnityWebRequest.EscapeURL(cursor ?? "")}",
            new PlayerProfilePage { items = null }, response => response.items != null && response.items.All(item => ValidProfile(item) && item.grade == grade));
        public Task<BattleHistoryListResponse> GetHistoryAsync(string name, string cursor) => Request("/battle-histories", $"&player={UnityWebRequest.EscapeURL(name)}&cursor={UnityWebRequest.EscapeURL(cursor ?? "")}",
            new BattleHistoryListResponse { items = null }, response => response.items != null && response.items.All(item => item != null && !string.IsNullOrWhiteSpace(item.id)));

        async Task<T> Request<T>(string route, string query, T response, Func<T, bool> valid, object body = null) where T : class {
            var url = $"{setting.CloudApiBaseUrl.TrimEnd('/')}/index.php?route={UnityWebRequest.EscapeURL(route)}{query}";
            using var request = new UnityWebRequest(url, body == null ? "GET" : "POST");
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null) {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = 8;
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success) throw new ApiRequestException(request.responseCode, $"Player records API: {request.responseCode} {request.error}");
            var contentType = request.GetResponseHeader("Content-Type");
            if (contentType == null || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)) throw new ApiRequestException(request.responseCode, "Player records API did not return JSON.");
            return ApiResponseParser.Read(request, response, valid);
        }
        static bool ValidProfile(PlayerProfile profile) => profile != null && !string.IsNullOrWhiteSpace(profile.name) && profile.grade >= 0 && profile.grade <= 5
            && ValidTimestamp(profile.createdAt) && ValidTimestamp(profile.acquiredAt)
            && (string.IsNullOrEmpty(profile.lastTrialAt) || ValidTimestamp(profile.lastTrialAt));
        static bool ValidTimestamp(string value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
