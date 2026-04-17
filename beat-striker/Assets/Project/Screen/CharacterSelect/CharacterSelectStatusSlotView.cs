using TMPro;
using UnityEngine;
using Alice;

public class CharacterSelectStatusSlotView : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] CanvasGroup blinkCanvasGroup;
    [SerializeField] TextMeshProUGUI playerLabel;
    [SerializeField] TextMeshProUGUI unselectedLabel;
    [SerializeField] CharacterSelectModelIcon modelIcon;
    [SerializeField] AudioClip unselectedSound;
    [SerializeField, Range(0f, 1f)] float unselectedSoundVolume = 1f;
    [SerializeField] Color[] playerLabelColors = { Color.white, Color.cyan, Color.green, Color.magenta };
    [SerializeField] Color cpuLabelColor = Color.yellow;
    [SerializeField, Range(0f, 1f)] float blinkMinAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] float blinkMaxAlpha = 1f;
    [SerializeField] float blinkDuration = 0.45f;

    bool hasRendered;
    bool wasSelected;
    bool isBlinking;

    void OnValidate()
    {
        if (playerLabelColors == null || playerLabelColors.Length == 0) {
            playerLabelColors = new[] { Color.white };
        }
    }

    public void SetVisible(bool visible)
    {
        root.SetActive(visible);
        if (!visible) {
            blinkCanvasGroup.alpha = blinkMaxAlpha;
            isBlinking = false;
        }
    }

    public void Render(CharacterSelectSlotState state)
    {
        var isSelected = state.IsSelected;

        playerLabel.text = state.HasGamePad ? $"{state.SlotIndex + 1}P" : "CPU";
        playerLabel.color = state.HasGamePad ? playerLabelColors[state.SlotIndex % playerLabelColors.Length] : cpuLabelColor;
        modelIcon.SetModel(state.SelectedModelPrefab);
        unselectedLabel.text = isSelected ? "選択済" : "未選択";

        if (!hasRendered) {
            ApplyBlinkState(isSelected);
            wasSelected = isSelected;
            hasRendered = true;
            return;
        }

        if (wasSelected != isSelected) {
            ApplyBlinkState(isSelected);
            if (!isSelected) {
                AudioSource.PlayClipAtPoint(unselectedSound, Camera.main.transform.position, unselectedSoundVolume);
            }
        }

        wasSelected = isSelected;
    }

    void ApplyBlinkState(bool isSelected)
    {
        blinkCanvasGroup.alpha = blinkMaxAlpha;
        isBlinking = true;
    }

    void Update()
    {
        if (!isBlinking || !root.activeSelf) {
            return;
        }

        if (blinkDuration <= 0f) {
            blinkCanvasGroup.alpha = blinkMaxAlpha;
            return;
        }

        var phase = Mathf.PingPong(Time.unscaledTime, blinkDuration) / blinkDuration;
        blinkCanvasGroup.alpha = Mathf.Lerp(blinkMaxAlpha, blinkMinAlpha, phase);
    }
}
