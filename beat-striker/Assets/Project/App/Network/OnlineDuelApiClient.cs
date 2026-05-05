using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using VContainer;

namespace Alice {
    public interface IOnlineDuelApiClient {
        Task<DuelPromptResponse> GetPromptsAsync(DuelPromptRequest request);
        Task<DuelInviteResponse> CreateInviteAsync(DuelInviteCreateRequest request);
        Task<DuelInviteResponse> AcceptInviteAsync(string inviteId, DuelInviteActionRequest request);
        Task RejectInviteAsync(string inviteId, DuelInviteActionRequest request);
        Task CancelInviteAsync(string inviteId, DuelInviteActionRequest request);
        Task<DuelReservationResponse> ConsumeReservationAsync(string reservationId, DuelReservationConsumeRequest request);
    }

    public class OnlineDuelApiClient : IOnlineDuelApiClient {
        readonly IAppNetworkSetting appNetworkSetting;

        [Inject]
        public OnlineDuelApiClient(IAppNetworkSetting appNetworkSetting) {
            this.appNetworkSetting = appNetworkSetting;
        }

        public async Task<DuelPromptResponse> GetPromptsAsync(DuelPromptRequest request) {
            using var webRequest = CreatePost(appNetworkSetting.CloudApiBaseUrl, "/duel/prompts", request);
            await SendAsync(webRequest);
            return JsonUtility.FromJson<DuelPromptResponse>(webRequest.downloadHandler.text);
        }

        public async Task<DuelInviteResponse> CreateInviteAsync(DuelInviteCreateRequest request) {
            using var webRequest = CreatePost(appNetworkSetting.CloudApiBaseUrl, "/invites", request);
            await SendAsync(webRequest);
            return JsonUtility.FromJson<DuelInviteResponse>(webRequest.downloadHandler.text);
        }

        public async Task<DuelInviteResponse> AcceptInviteAsync(string inviteId, DuelInviteActionRequest request) {
            using var webRequest = CreatePost(appNetworkSetting.CloudApiBaseUrl, $"/invites/{UnityWebRequest.EscapeURL(inviteId)}/accept", request);
            await SendAsync(webRequest);
            return JsonUtility.FromJson<DuelInviteResponse>(webRequest.downloadHandler.text);
        }

        public async Task RejectInviteAsync(string inviteId, DuelInviteActionRequest request) {
            using var webRequest = CreatePost(appNetworkSetting.CloudApiBaseUrl, $"/invites/{UnityWebRequest.EscapeURL(inviteId)}/reject", request);
            await SendAsync(webRequest);
        }

        public async Task CancelInviteAsync(string inviteId, DuelInviteActionRequest request) {
            using var webRequest = CreatePost(appNetworkSetting.CloudApiBaseUrl, $"/invites/{UnityWebRequest.EscapeURL(inviteId)}/cancel", request);
            await SendAsync(webRequest);
        }

        public async Task<DuelReservationResponse> ConsumeReservationAsync(string reservationId, DuelReservationConsumeRequest request) {
            using var webRequest = CreatePost(appNetworkSetting.CloudApiBaseUrl, $"/reservations/{UnityWebRequest.EscapeURL(reservationId)}/consume", request);
            await SendAsync(webRequest);
            return JsonUtility.FromJson<DuelReservationResponse>(webRequest.downloadHandler.text);
        }

        static UnityWebRequest CreatePost<T>(string baseUrl, string path, T payload) {
            var json = JsonUtility.ToJson(payload);
            var webRequest = new UnityWebRequest($"{baseUrl}{path}", UnityWebRequest.kHttpVerbPOST);
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            return webRequest;
        }

        static async Task SendAsync(UnityWebRequest webRequest) {
            var operation = webRequest.SendWebRequest();
            while (!operation.isDone) {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success) {
                throw new InvalidOperationException($"Online duel API failed. url={webRequest.url}, status={webRequest.responseCode}, error={webRequest.error}, body={webRequest.downloadHandler?.text}");
            }
        }
    }
}
