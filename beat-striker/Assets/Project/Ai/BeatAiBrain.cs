using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public enum InputDirectionType {
        None,
        Deg0,
        Deg15,
        Deg30,
        Deg45,
        Deg60,
        Deg75,
        Deg90,
        Deg105,
        Deg120,
        Deg135,
        Deg150,
        Deg165,
        Deg180,
        Deg195,
        Deg210,
        Deg225,
        Deg240,
        Deg255,
        Deg270,
        Deg285,
        Deg300,
        Deg315,
        Deg330,
        Deg345
    }

    public enum OptionalGamePadButton {
        None = -1,
        North = (int)GamePadButton.North,
        South = (int)GamePadButton.South,
        West = (int)GamePadButton.West,
        East = (int)GamePadButton.East,
        Right = (int)GamePadButton.Right,
        Left = (int)GamePadButton.Left,
        Start = (int)GamePadButton.Start,
        Select = (int)GamePadButton.Select,
    }

    [Serializable]
    public class AiActionSequenceItem {
        public OptionalGamePadButton Button = OptionalGamePadButton.None;
        public InputDirectionType Direction = InputDirectionType.None;

        public Vector2 GetDirectionVector() {
            if (Direction == InputDirectionType.None) {
                return Vector2.zero;
            }
            int angle = ((int)Direction - 1) * 15;
            return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        }

        public GamePadButton? GetButton() {
            if (Button == OptionalGamePadButton.None) {
                return null;
            }
            return (GamePadButton)Button;
        }
    }

    public class BeatAiBrain : AiBrain {
        [SerializeField] bool isRandomSequence = false;
        [SerializeField] List<AiActionSequenceItem> actionSequence = new List<AiActionSequenceItem>();

        int currentActionIndex = 0;

        protected override AiAction OnGoodWindow(AiObservation observation) {
            if (actionSequence == null || actionSequence.Count == 0) {
                return AiAction.None;
            }

            AiActionSequenceItem item;
            if (isRandomSequence) {
                int randomIndex = UnityEngine.Random.Range(0, actionSequence.Count);
                item = actionSequence[randomIndex];
            } else {
                item = actionSequence[currentActionIndex];
                currentActionIndex = (currentActionIndex + 1) % actionSequence.Count;
            }

            return new AiAction(item.GetDirectionVector(), item.GetButton());
        }

        protected override void OnAiEnabled() {
            currentActionIndex = 0;
        }

        protected override void OnAiDisabled() {
            currentActionIndex = 0;
        }
    }
}