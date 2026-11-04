using TMPro;
using UnityEngine;

namespace Alice {
    public sealed class TutorialBandLayoutView : MonoBehaviour {
        [SerializeField] RectTransform band;
        [SerializeField] TMP_Text body;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text continueHint;
        [SerializeField, Min(0f)] float verticalPadding = 7f;
        [SerializeField, Min(0f)] float rowSpacing = 3f;
        [SerializeField, Min(0f)] float horizontalPadding = 24f;
        string previousText;
        float previousWidth;
        void OnEnable() { previousText = null; Refresh(); }
        void LateUpdate() => Refresh();
        public void Refresh(float widthOverride = 0f) {
            var bandWidth = widthOverride > 0f ? widthOverride : band.rect.width;
            if (bandWidth <= horizontalPadding * 2f) return;
            var signature = body.text + (title != null && title.gameObject.activeSelf ? title.text : "") +
                (continueHint != null && continueHint.gameObject.activeSelf ? continueHint.text : "");
            if (signature == previousText && Mathf.Approximately(previousWidth, bandWidth)) return;
            previousText = signature;
            previousWidth = bandWidth;
            var width = bandWidth - horizontalPadding * 2f;
            var bodyHeight = body.GetPreferredValues(body.text, width, float.PositiveInfinity).y;
            var titleHeight = title != null && title.gameObject.activeSelf ? title.GetPreferredValues(title.text, width, float.PositiveInfinity).y : 0f;
            var hintHeight = continueHint != null && continueHint.gameObject.activeSelf ? continueHint.GetPreferredValues(continueHint.text, width, float.PositiveInfinity).y : 0f;
            var height = bodyHeight + titleHeight + hintHeight + verticalPadding * 2f +
                (titleHeight > 0f ? rowSpacing : 0f) + (hintHeight > 0f ? rowSpacing : 0f);
            band.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var top = height * 0.5f - verticalPadding;
            if (titleHeight > 0f) {
                Place(title, titleHeight, top - titleHeight * 0.5f);
                top -= titleHeight + rowSpacing;
            }
            Place(body, bodyHeight, top - bodyHeight * 0.5f);
            if (hintHeight > 0f) Place(continueHint, hintHeight, -height * 0.5f + verticalPadding + hintHeight * 0.5f);
        }
        void Place(TMP_Text text, float height, float y) {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(-horizontalPadding * 2f, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }
    }
}
