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
        [SerializeField, Tooltip("上/下 Botan ホバー中のスクロール速度（normalized / 秒）。")]
        float battleHistoryScrollNormalizedSpeed = 1.5f;
        public Observable<Unit> BackToMenuRequested => backToMenuRequested;

        void Awake() {
            BuildScreen();
            backToMenuEmitter.OnClickEvent.Subscribe(_ => backToMenuRequested.OnNext(Unit.Default)).AddTo(this);
            battleHistoryScrollUpHoverBotan.OnHoverEvent.Subscribe(_ => battleHistoryScrollUpHovered.Value = true).AddTo(this);
            battleHistoryScrollUpHoverBotan.OnHoverExitEvent.Subscribe(_ => battleHistoryScrollUpHovered.Value = false).AddTo(this);
            battleHistoryScrollDownHoverBotan.OnHoverEvent.Subscribe(_ => battleHistoryScrollDownHovered.Value = true).AddTo(this);
            battleHistoryScrollDownHoverBotan.OnHoverExitEvent.Subscribe(_ => battleHistoryScrollDownHovered.Value = false).AddTo(this);
        }

        void BuildScreen() {
            var canvas = battleHistoryScrollRect.transform.parent;
            var bodyFont = ((RecyclableScrollRect)battleHistoryScrollRect).PrototypeCell.GetComponentInChildren<TextMeshProUGUI>().font;
            if (!displayFont) displayFont = bodyFont;
            var background = RankingUi.Band(canvas, "Ranking Backdrop", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, RankingUi.Ink, RankingUi.Ink);
            background.transform.SetAsFirstSibling();
            var left = RankingUi.Rect(canvas, "Ranking Title Panel", Vector2.zero, new Vector2(0.34f, 1), Vector2.zero, Vector2.zero);
            RankingUi.Band(left, "Orange to Yellow", Vector2.zero, Vector2.one, new Vector2(-55, -15), new Vector2(0, 15), new Color32(255, 130, 0, 255), RankingUi.Yellow, 70);
            for (var i = 0; i < 8; i++) {
                var stripe = RankingUi.Band(left, "Speed Line", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(-55, i * 31 - 135), new Vector2(0, i * 31 - 133), new Color(1, 1, 1, 0.18f), new Color(1, 1, 1, 0.02f));
                stripe.rectTransform.localEulerAngles = new Vector3(0, 0, 18);
            }
            var iconRect = RankingUi.Rect(left, "Menu Trophy", new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(-135, -125), new Vector2(115, 125));
            iconRect.localEulerAngles = new Vector3(0, 0, 13);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = trophy; icon.preserveAspect = true; icon.raycastTarget = false;
            icon.color = new Color32(255, 247, 121, 255);
            var titleBand = RankingUi.Band(left, "Title Black Slash", new Vector2(0, 0.19f), new Vector2(1, 0.39f), new Vector2(-12, 0), new Vector2(-15, 0), RankingUi.Ink, RankingUi.Ink, 22);
            titleBand.rectTransform.localEulerAngles = new Vector3(0, 0, 7);
            var title = RankingUi.Text(titleBand.transform, "ランキング", displayFont, "ランキング", Vector2.zero, Vector2.one, new Vector2(17, 0), new Vector2(-8, 0), 47, Color.white, TextAlignmentOptions.Center);
            title.enableAutoSizing = true; title.fontSizeMin = 30; title.fontSizeMax = 47;
            RankingUi.Text(left, "Replay Invitation", displayFont, "あのバトルをもういちど！", new Vector2(0, 0.08f), new Vector2(1, 0.19f), new Vector2(12, 0), new Vector2(-30, 0), 21, RankingUi.Ink, TextAlignmentOptions.Center);
            var heading = RankingUi.Text(canvas, "対戦履歴", displayFont, "バトルのきろく", new Vector2(0.36f, 1), new Vector2(0.80f, 1), new Vector2(5, -74), new Vector2(0, -6), 38, Color.white);
            heading.rectTransform.localEulerAngles = new Vector3(0, 0, 3);
            RankingUi.Text(canvas, "History Limit", bodyFont, "最新 50 戦", new Vector2(0.76f, 1), new Vector2(1, 1), new Vector2(0, -66), new Vector2(-42, -20), 17, RankingUi.Yellow, TextAlignmentOptions.MidlineRight);
            var scroll = (RectTransform)battleHistoryScrollRect.transform;
            scroll.anchorMin = new Vector2(0.35f, 0); scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(0, 48); scroll.offsetMax = new Vector2(-46, -87);
            scroll.localScale = Vector3.one;
            var scrollImage = scroll.GetComponent<Image>(); scrollImage.color = Color.clear; scrollImage.sprite = null;
            battleHistoryScrollRect.scrollSensitivity = 35;
            var status = RankingUi.Text(canvas, "History Status", bodyFont, "", new Vector2(0.36f, 0.35f), new Vector2(0.95f, 0.68f), new Vector2(15, 0), new Vector2(-15, 0), 28, Color.white, TextAlignmentOptions.Center);
            FindFirstObjectByType<RankingHistoryListView>().SetStatusLabel(status);
            ConfigureScrollHint(battleHistoryScrollUpHoverBotan, true);
            ConfigureScrollHint(battleHistoryScrollDownHoverBotan, false);
            RankingUi.Text(canvas, "Replay Help", bodyFont, "対戦を選んでリプレイ", new Vector2(0.35f, 0), new Vector2(1, 0), new Vector2(8, 4), new Vector2(-50, 40), 16, Color.white, TextAlignmentOptions.Center);
            backToMenuEmitter.transform.SetAsLastSibling();
        }

        void ConfigureScrollHint(Botan botan, bool up) {
            var rect = (RectTransform)botan.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, up ? 1 : 0);
            rect.anchoredPosition = new Vector2(-23, up ? -110 : 74); rect.sizeDelta = new Vector2(35, 48);
            var image = botan.GetComponent<Image>(); image.sprite = null; image.color = RankingUi.Yellow;
            var arrow = RankingUi.Band(rect, "Arrow", Vector2.zero, Vector2.one, new Vector2(9, 16), new Vector2(-9, -16), RankingUi.Ink, RankingUi.Ink);
            arrow.triangle = true; arrow.pointUp = up; arrow.SetVerticesDirty();
            botan.OnHoverEvent.Subscribe(_ => image.color = Color.white).AddTo(this);
            botan.OnHoverExitEvent.Subscribe(_ => image.color = RankingUi.Yellow).AddTo(this);
            rect.SetAsLastSibling();
        }

        void Update() {
            var scroll = battleHistoryScrollRect;
            if (!scroll || !scroll.vertical || !scroll.isActiveAndEnabled) return;
            var direction = (battleHistoryScrollUpHovered.CurrentValue ? 1 : 0) - (battleHistoryScrollDownHovered.CurrentValue ? 1 : 0);
            if (direction != 0) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + direction * battleHistoryScrollNormalizedSpeed * Time.deltaTime);
        }

        void OnDisable() {
            battleHistoryScrollUpHovered.Value = false;
            battleHistoryScrollDownHovered.Value = false;
        }
    }
}
