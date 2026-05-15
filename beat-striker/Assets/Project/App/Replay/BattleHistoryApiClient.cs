using System;
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
            using var webRequest = new UnityWebRequest($"{appNetworkSetting.CloudApiBaseUrl}/battle-histories", UnityWebRequest.kHttpVerbPOST);
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            await SendAsync(webRequest);
            return JsonUtility.FromJson<BattleHistoryCreateResponse>(webRequest.downloadHandler.text);
        }

        public async Task<BattleHistorySummary[]> GetSummariesAsync(int limit) {
            using var webRequest = UnityWebRequest.Get($"{appNetworkSetting.CloudApiBaseUrl}/battle-histories?limit={Mathf.Clamp(limit, 1, 100)}");
            await SendAsync(webRequest);
            var response = JsonUtility.FromJson<BattleHistoryListResponse>(webRequest.downloadHandler.text);
            return response?.items ?? Array.Empty<BattleHistorySummary>();
        }

        public async Task<BattleHistoryDetail> GetDetailAsync(string id) {
            using var webRequest = UnityWebRequest.Get($"{appNetworkSetting.CloudApiBaseUrl}/battle-histories/{UnityWebRequest.EscapeURL(id)}");
            await SendAsync(webRequest);
            return JsonUtility.FromJson<BattleHistoryDetail>(webRequest.downloadHandler.text);
        }

        static async Task SendAsync(UnityWebRequest webRequest) {
            var operation = webRequest.SendWebRequest();
            while (!operation.isDone) {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success) {
                throw new InvalidOperationException($"Battle history API failed. url={webRequest.url}, status={webRequest.responseCode}, error={webRequest.error}, body={webRequest.downloadHandler?.text}");
            }
        }
    }
}
