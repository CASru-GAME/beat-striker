using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using VContainer;

namespace Alice {
    public interface IBattleHistoryApiClient {
        Task<BattleHistoryCreateResponse> SaveAsync(BattleHistorySaveRequest request);
        Task<BattleHistorySummary[]> GetSummariesAsync(int limit);
        Task<BattleHistoryDetail> GetDetailAsync(string id);
    }

    public class BattleHistoryApiClient : IBattleHistoryApiClient {
        readonly IAppNetworkSetting appNetworkSetting;

        [Inject]
        public BattleHistoryApiClient(IAppNetworkSetting appNetworkSetting) {
            this.appNetworkSetting = appNetworkSetting;
        }

        public async Task<BattleHistoryCreateResponse> SaveAsync(BattleHistorySaveRequest request) {
            var json = JsonUtility.ToJson(request);
            using var webRequest = new UnityWebRequest(BuildUrl("/battle-histories"), UnityWebRequest.kHttpVerbPOST);
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            await SendAsync(webRequest);
            return ApiResponseParser.Read(webRequest, new BattleHistoryCreateResponse(), response => response.ok && !string.IsNullOrWhiteSpace(response.id));
        }

        public async Task<BattleHistorySummary[]> GetSummariesAsync(int limit) {
            using var webRequest = UnityWebRequest.Get($"{BuildUrl("/battle-histories")}&limit={Mathf.Clamp(limit, 1, 100)}");
            await SendAsync(webRequest);
            return ApiResponseParser.Read(webRequest, new BattleHistoryListResponse { items = null }, response => response.items != null
                && response.items.All(item => item != null && !string.IsNullOrWhiteSpace(item.id))).items;
        }

        public async Task<BattleHistoryDetail> GetDetailAsync(string id) {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Battle history ID is required.", nameof(id));
            using var webRequest = UnityWebRequest.Get(BuildUrl($"/battle-histories/{id}"));
            await SendAsync(webRequest);
            return ApiResponseParser.Read(webRequest, new BattleHistoryDetail(), response => !string.IsNullOrWhiteSpace(response.id)
                && response.replayPayload != null && response.replayPayload.schemaVersion == 1 && response.replayPayload.rounds != null);
        }

        string BuildUrl(string route) {
            var baseUrl = appNetworkSetting.CloudApiBaseUrl;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) {
                throw new InvalidOperationException("Battle history API base URL must be an HTTP(S) folder URL without query or fragment.");
            }
            return $"{baseUrl.TrimEnd('/')}/index.php?route={UnityWebRequest.EscapeURL(route)}";
        }

        static async Task SendAsync(UnityWebRequest webRequest) {
            webRequest.timeout = 30;
            webRequest.SetRequestHeader("Accept", "application/json");
            var operation = webRequest.SendWebRequest();
            while (!operation.isDone) {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success) {
                throw new ApiRequestException(webRequest.responseCode, $"Battle history API failed. url={webRequest.url}, status={webRequest.responseCode}, error={webRequest.error}, body={webRequest.downloadHandler?.text}");
            }
            var contentType = webRequest.GetResponseHeader("Content-Type");
            if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)) {
                throw new ApiRequestException(webRequest.responseCode, $"Battle history API did not return JSON. Check the PHP API installation. url={webRequest.url}");
            }
        }
    }
}
