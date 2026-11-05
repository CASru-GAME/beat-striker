using Core;
using R3;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PolyAndCode.UI;

namespace Alice {
    public class RankingPresenterView : MonoBehaviour {
        readonly Subject<Unit> backToMenuRequested = new();
        readonly ReactiveProperty<bool> battleHistoryScrollUpHovered = new(false);
        readonly ReactiveProperty<bool> battleHistoryScrollDownHovered = new(false);
        [SerializeField] ActionEmitter backToMenuEmitter;
        [SerializeField] Botan battleHistoryScrollUpHoverBotan;
        [SerializeField] Botan battleHistoryScrollDownHoverBotan;
        [SerializeField] ScrollRect battleHistoryScrollRect;
        [SerializeField] TMP_FontAsset displayFont;
        [SerializeField] Sprite trophy;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField, Tooltip("上/下 Botan ホバー中のスクロール速度（normalized / 秒）。")]
        float battleHistoryScrollNormalizedSpeed = 1.5f;
        [SerializeField, Min(0)] float edgeScrollZoneHeight = 48f;
        [SerializeField, Min(0)] float edgeScrollSpeed = 500f;
        [SerializeField, Range(0, 1)] float historyLoadThreshold = 0.15f;
        [SerializeField] RankingHistoryListView historyList;
        [SerializeField] RankingGradeSectionView[] gradeSections;
        [SerializeField] GameObject historyDialog;
        [SerializeField] TMP_Text historyHeading;
        [SerializeField] TMP_Text rankingStatus;
        [SerializeField] TMP_Text rankingHelp;
        [SerializeField] ActionEmitter closeHistoryEmitter;
        [SerializeField] ScrollRect gradeScroll;
        readonly Subject<string> playerSelected = new();
        readonly Subject<Unit> closeHistoryRequested = new(), moreHistoryRequested = new();
        bool hasMoreHistory;
        public Transform HistoryRoot => historyDialog.transform;
        public bool InputAllowed { private get; set; } = true;
        public Observable<string> PlayerSelected => playerSelected;
        public Observable<Unit> CloseHistoryRequested => closeHistoryRequested;
        public Observable<Unit> MoreHistoryRequested => moreHistoryRequested;
        public float ListPosition { get => gradeScroll.verticalNormalizedPosition; set => gradeScroll.verticalNormalizedPosition = value; }
        public float HistoryPosition { get => battleHistoryScrollRect.verticalNormalizedPosition; set => battleHistoryScrollRect.verticalNormalizedPosition = value; }
        public void SetRanking(System.Collections.Generic.IEnumerable<RankingPlayerEntry> entries) {
            var list = new System.Collections.Generic.List<RankingPlayerEntry>(entries);
            foreach (var section in gradeSections) section.Set(list.FindAll(row => row.Grade == section.Grade), playerSelected.OnNext);
        }
        public void SetRankingStatus(string text) {
            var showingStatus = !string.IsNullOrEmpty(text);
            rankingStatus.text = text;
            rankingStatus.gameObject.SetActive(showingStatus);
            rankingHelp.gameObject.SetActive(!showingStatus);
        }
        public void ShowHistory(string name, bool more) {
            historyHeading.richText = false; historyHeading.text = name + " の対戦履歴";
            hasMoreHistory = more; historyDialog.SetActive(true);
        }
        public void CloseHistory() {
            hasMoreHistory = false;
            battleHistoryScrollRect.StopMovement();
            battleHistoryScrollUpHovered.OnNext(false);
            battleHistoryScrollDownHovered.OnNext(false);
            historyDialog.SetActive(false);
        }
        public Observable<Unit> BackToMenuRequested => backToMenuRequested;

        void Awake() {
            historyList.SetStatusLabel(statusLabel);
            closeHistoryEmitter.OnClickEvent.Subscribe(_ => closeHistoryRequested.OnNext(Unit.Default)).AddTo(this);
            BindScrollHint(battleHistoryScrollUpHoverBotan);
            BindScrollHint(battleHistoryScrollDownHoverBotan);
            backToMenuEmitter.OnClickEvent.Subscribe(_ => backToMenuRequested.OnNext(Unit.Default)).AddTo(this);
            battleHistoryScrollUpHoverBotan.OnHoverEvent.Subscribe(_ => battleHistoryScrollUpHovered.OnNext(true)).AddTo(this);
            battleHistoryScrollUpHoverBotan.OnHoverExitEvent.Subscribe(_ => battleHistoryScrollUpHovered.OnNext(false)).AddTo(this);
            battleHistoryScrollDownHoverBotan.OnHoverEvent.Subscribe(_ => battleHistoryScrollDownHovered.OnNext(true)).AddTo(this);
            battleHistoryScrollDownHoverBotan.OnHoverExitEvent.Subscribe(_ => battleHistoryScrollDownHovered.OnNext(false)).AddTo(this);
        }

        void BindScrollHint(Botan botan) {
            var image = botan.GetComponent<Image>();
            botan.OnHoverEvent.Subscribe(_ => image.color = Color.white).AddTo(this);
            botan.OnHoverExitEvent.Subscribe(_ => image.color = RankingUi.Yellow).AddTo(this);
        }

        void Update() {
            if (!InputAllowed) { battleHistoryScrollUpHovered.OnNext(false); battleHistoryScrollDownHovered.OnNext(false); return; }
            if (historyDialog.activeSelf && hasMoreHistory &&
                (battleHistoryScrollRect.verticalNormalizedPosition <= historyLoadThreshold ||
                 battleHistoryScrollRect.content.rect.height <= battleHistoryScrollRect.viewport.rect.height)) {
                hasMoreHistory = false;
                moreHistoryRequested.OnNext(Unit.Default);
            }
            var scroll = historyDialog.activeSelf ? battleHistoryScrollRect : gradeScroll;
            if (!scroll || !scroll.vertical || !scroll.isActiveAndEnabled || !scroll.content) return;
            var direction = (battleHistoryScrollUpHovered.CurrentValue ? 1 : 0) - (battleHistoryScrollDownHovered.CurrentValue ? 1 : 0);
            var viewport = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
            var canvas = viewport.GetComponentInParent<Canvas>();
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var edgeDirection = 0;
            foreach (var cursor in Cursor.ActiveCursors) {
                if (cursor.IsHoveringButton) continue;
                var cursorCanvas = cursor.GetComponentInParent<Canvas>();
                var cursorCamera = cursorCanvas && cursorCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cursorCanvas.worldCamera : null;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(cursorCamera, cursor.transform.position);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPoint, camera, out var point) || point.x < viewport.rect.xMin || point.x > viewport.rect.xMax) continue;
                if (point.y >= viewport.rect.yMax - edgeScrollZoneHeight) edgeDirection++;
                else if (point.y <= viewport.rect.yMin + edgeScrollZoneHeight) edgeDirection--;
            }
            if (edgeDirection != 0) {
                var scrollableHeight = scroll.content.rect.height - viewport.rect.height;
                if (scrollableHeight > 0) {
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + Mathf.Sign(edgeDirection) * edgeScrollSpeed * Time.deltaTime / scrollableHeight);
                }
            } else if (direction != 0) {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + direction * battleHistoryScrollNormalizedSpeed * Time.deltaTime);
            }
        }

        void OnDisable() {
            battleHistoryScrollUpHovered.OnNext(false);
            battleHistoryScrollDownHovered.OnNext(false);
        }
    }
}
