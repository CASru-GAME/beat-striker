using UnityEngine;

namespace Alice {
    public class BeatAiBrain : AiBrain {
        [SerializeField] GamePadButton[] buttonPattern = { GamePadButton.East, GamePadButton.South, GamePadButton.West, GamePadButton.North };

        protected override GamePadButton OnBeat(int beatIndex) {
            return buttonPattern[beatIndex % buttonPattern.Length];
        }
    }
}