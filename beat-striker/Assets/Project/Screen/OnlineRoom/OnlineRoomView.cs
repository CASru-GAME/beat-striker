using System;
using System.Collections.Generic;
using Core;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class OnlineRoomView : MonoBehaviour {
        [SerializeField] Botan backButton;
        [SerializeField] Botan refreshButton;
        [SerializeField] Botan challengeButton;
        [SerializeField] Botan acceptButton;
        [SerializeField] Botan rejectButton;
        [SerializeField] Botan cancelButton;
        [SerializeField] Botan scrollUpButton;
        [SerializeField] Botan scrollDownButton;
        [SerializeField] ScrollRect playerScroll;
        [SerializeField] RectTransform playerContent;
        [SerializeField] OnlineRoomPlayerView playerPrefab;
        [SerializeField] TextMeshProUGUI countLabel;
        [SerializeField] TextMeshProUGUI connectionLabel;
        [SerializeField] TextMeshProUGUI emptyLabel;
        [SerializeField] TextMeshProUGUI opponentLabel;
        [SerializeField] GradeBadgeView opponentGrade;
        [SerializeField] TextMeshProUGUI opponentStatusLabel;
        [SerializeField] TextMeshProUGUI messageLabel;
        [SerializeField] TextMeshProUGUI challengeLabel;
        [SerializeField] CanvasGroup challengeGroup;
        [SerializeField] GameObject incomingActions;
        [SerializeField] GameObject cancelAction;
        readonly Dictionary<string, OnlineRoomPlayerView> rows = new();
        readonly Subject<Unit> back = new();
        readonly Subject<Unit> refresh = new();
        readonly Subject<Unit> challenge = new();
        readonly Subject<Unit> accept = new();
        readonly Subject<Unit> reject = new();
        readonly Subject<Unit> cancel = new();
        readonly Subject<string> selected = new();
        bool canChallenge;
        bool scrollUp;
        bool scrollDown;

        [SerializeField] FocusGraphicRaycaster focusRaycaster;
        [SerializeField] Transform duelPanel;
        [SerializeField] FocusGraphicRaycaster duelRaycaster;
        public bool IsDuelPending { get; private set; }
        public Transform DuelRoot => duelPanel;
        public FocusGraphicRaycaster InputRaycaster => focusRaycaster;
        public FocusGraphicRaycaster DuelRaycaster => duelRaycaster;
        public bool InputAllowed { private get; set; } = true;
        public Observable<Unit> BackRequested => back;
        public Observable<Unit> RefreshRequested => refresh;
        public Observable<Unit> ChallengeRequested => challenge;
        public Observable<Unit> AcceptRequested => accept;
        public Observable<Unit> RejectRequested => reject;
        public Observable<Unit> CancelRequested => cancel;
        public Observable<string> PlayerSelected => selected;

        void Awake() {
            backButton.OnClickEvent.Subscribe(_ => back.OnNext(Unit.Default)).AddTo(this);
            refreshButton.OnClickEvent.Subscribe(_ => refresh.OnNext(Unit.Default)).AddTo(this);
            challengeButton.OnClickEvent.Subscribe(_ => { if (canChallenge) challenge.OnNext(Unit.Default); }).AddTo(this);
            acceptButton.OnClickEvent.Subscribe(_ => accept.OnNext(Unit.Default)).AddTo(this);
            rejectButton.OnClickEvent.Subscribe(_ => reject.OnNext(Unit.Default)).AddTo(this);
            cancelButton.OnClickEvent.Subscribe(_ => cancel.OnNext(Unit.Default)).AddTo(this);
            scrollUpButton.OnHoverEvent.Subscribe(_ => scrollUp = true).AddTo(this);
            scrollUpButton.OnHoverExitEvent.Subscribe(_ => scrollUp = false).AddTo(this);
            scrollDownButton.OnHoverEvent.Subscribe(_ => scrollDown = true).AddTo(this);
            scrollDownButton.OnHoverExitEvent.Subscribe(_ => scrollDown = false).AddTo(this);
        }

        public void Render(OnlineRoomRoster roster, OnlineDuelUiState state, string selection, bool busy, string feedback) {
            var players = roster.Players ?? Array.Empty<OnlineRoomPlayer>();
            var remaining = new HashSet<string>(rows.Keys);
            OnlineRoomPlayer chosen = null;
            for (var i = 0; i < players.Length; i++) {
                var player = players[i];
                if (string.IsNullOrEmpty(player.sessionId)) continue;
                remaining.Remove(player.sessionId);
                if (!rows.TryGetValue(player.sessionId, out var row)) {
                    // Only repeated player entries are instantiated; the screen is authored in the scene.
                    row = Instantiate(playerPrefab, playerContent);
                    rows.Add(player.sessionId, row);
                }
                row.transform.SetSiblingIndex(i);
                row.Bind(player, i, player.sessionId == selection, id => selected.OnNext(id));
                if (player.sessionId == selection) chosen = player;
            }
            foreach (var id in remaining) {
                rows[id].gameObject.SetActive(false);
                Destroy(rows[id].gameObject);
                rows.Remove(id);
            }
            countLabel.text = roster.IsConnected ? $"{players.Length + 1:00} ONLINE" : "-- ONLINE";
            connectionLabel.text = roster.IsConnected ? "接続中 / LIVE" : "接続を確認中";
            emptyLabel.gameObject.SetActive(players.Length == 0);
            emptyLabel.text = roster.IsConnected ? "対戦相手を待っています\n<size=18>入室するとここに表示されます</size>" : "接続者を確認中";
            var incoming = state.UiMode == OnlineDuelUiMode.IncomingInvite;
            var outgoing = state.UiMode == OnlineDuelUiMode.InviteSent;
            var matched = state.HasReservation;
            IsDuelPending = incoming || outgoing || matched || busy;
            var opponent = incoming ? state.InviteFromSessionId : outgoing || matched ? state.OpponentSessionId : chosen?.sessionId;
            opponentLabel.richText = false;
            var opponentEntry = Array.Find(roster.Players, entry => entry.sessionId == opponent);
            opponentLabel.text = string.IsNullOrEmpty(opponent) ? "対戦相手を選べ！" : opponentEntry?.player == null ? PlayerName(opponent) : opponentEntry.player.name;
            opponentGrade.gameObject.SetActive(opponentEntry?.player != null);
            if (opponentEntry?.player != null) opponentGrade.Set(opponentEntry.player.grade);
            opponentStatusLabel.text = incoming ? "挑戦状が届いた！" : outgoing ? "相手の返答を待っています" : matched ? "対戦成立！" : chosen == null ? "左のプレイヤーを選択" : $"{SceneName(chosen.scene)} / {(chosen.canInvite ? "対戦受付中" : "取り込み中")}";
            canChallenge = roster.IsConnected && chosen != null && chosen.canInvite && !incoming && !outgoing && !matched && !busy;
            challengeGroup.alpha = canChallenge ? 1 : 0.38f;
            challengeGroup.blocksRaycasts = canChallenge;
            challengeLabel.text = busy ? "送信中" : "宣戦布告";
            challengeButton.gameObject.SetActive(!incoming && !outgoing && !matched);
            incomingActions.SetActive(incoming);
            cancelAction.SetActive(outgoing);
            messageLabel.text = !string.IsNullOrEmpty(feedback) ? feedback : incoming ? "受けて立つ？" : outgoing ? "挑戦状を送った！" : matched ? "ステージ選択へ！" : "相手を選んで、バトルを仕掛けろ！";
        }

        void Update() {
            if (!InputAllowed) { scrollUp = scrollDown = false; return; }
            if (!playerScroll.content || !playerScroll.viewport) return;
            var height = playerContent.rect.height - playerScroll.viewport.rect.height;
            if (height <= 0) return;
            var direction = (scrollUp ? 1 : 0) - (scrollDown ? 1 : 0);
            var canvas = playerScroll.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var cursor in Cursor.ActiveCursors) {
                var cursorCanvas = cursor.GetComponentInParent<Canvas>();
                var cursorCamera = cursorCanvas && cursorCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cursorCanvas.worldCamera : null;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(cursorCamera, cursor.transform.position);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(playerScroll.viewport, screenPoint, camera, out var point)
                    || point.x < playerScroll.viewport.rect.xMin || point.x > playerScroll.viewport.rect.xMax) continue;
                if (point.y >= playerScroll.viewport.rect.yMax - 32 && point.y <= playerScroll.viewport.rect.yMax + 32) direction++;
                if (point.y <= playerScroll.viewport.rect.yMin + 32 && point.y >= playerScroll.viewport.rect.yMin - 32) direction--;
            }
            if (direction == 0) return;
            playerScroll.StopMovement();
            playerScroll.verticalNormalizedPosition = Mathf.Clamp01(playerScroll.verticalNormalizedPosition + Mathf.Sign(direction) * 480 * Time.unscaledDeltaTime / height);
        }

        void OnDisable() { scrollUp = scrollDown = false; }

        public static string PlayerName(string id) => string.IsNullOrEmpty(id) ? "PLAYER" : $"PLAYER_{id.Substring(0, Math.Min(8, id.Length)).ToUpperInvariant()}";

        static string SceneName(string scene) => scene switch {
            "OnlineRoom" => "バトルルーム",
            "StageSelect" => "ステージ選択",
            "CharacterSelect" => "キャラ選択",
            "Live" or "Street" => "バトル中",
            "Ranking" => "ランキング",
            "Menu" => "メニュー",
            "Title" => "タイトル",
            _ => "オンライン",
        };
    }
}
