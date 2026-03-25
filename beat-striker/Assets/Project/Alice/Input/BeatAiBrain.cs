using UnityEngine;

namespace Alice {
    public class BeatAiBrain : AiBrain {
        [SerializeField] GamePadButton[] buttonPattern = { GamePadButton.East, GamePadButton.South, GamePadButton.West, GamePadButton.North };
        int beatIndex = 0;

        protected override void OnGoodZoneEntered() {
            var button = buttonPattern[beatIndex % buttonPattern.Length];
            Press(button);
            beatIndex++;
        }
    }
}