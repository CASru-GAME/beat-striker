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
        public bool IsRandomSequence = false;
        public List<AiActionSequenceItem> SequenceItems = new();
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
        [SerializeField] AiActionSequenceItem rootSequence = new() { IsRandomSequence = false };
        readonly Dictionary<AiActionSequenceItem, int> sequentialIndices = new();

        public void SetActionSequence(AiActionSequenceItem sequence) {
            rootSequence = CloneSequence(sequence) ?? new AiActionSequenceItem();
            ResetRuntimeState();
        }

        protected override AiAction OnGoodWindow(AiObservation observation) {
            var actionItem = ResolveNextActionItem(rootSequence);
            if (actionItem == null) {
                return AiAction.None;
            }

            return new AiAction(actionItem.GetDirectionVector(), actionItem.GetButton());
        }

        protected override void OnAiEnabled() {
            ResetRuntimeState();
        }

        protected override void OnAiDisabled() {
            ResetRuntimeState();
        }

        void ResetRuntimeState() {
            sequentialIndices.Clear();
        }

        AiActionSequenceItem ResolveNextActionItem(AiActionSequenceItem node) {
            if (node == null) {
                return null;
            }

            if (node.SequenceItems == null || node.SequenceItems.Count == 0) {
                return node;
            }

            var selectedChild = node.IsRandomSequence
                ? SelectRandomChild(node.SequenceItems)
                : SelectSequentialChild(node);

            return ResolveNextActionItem(selectedChild);
        }

        AiActionSequenceItem SelectRandomChild(List<AiActionSequenceItem> children) {
            if (children == null || children.Count == 0) {
                return null;
            }

            var randomIndex = UnityEngine.Random.Range(0, children.Count);
            return children[randomIndex];
        }

        AiActionSequenceItem SelectSequentialChild(AiActionSequenceItem node) {
            if (!sequentialIndices.TryGetValue(node, out var currentIndex)) {
                currentIndex = 0;
            }

            var clampedIndex = Mathf.Clamp(currentIndex, 0, node.SequenceItems.Count - 1);
            var selectedChild = node.SequenceItems[clampedIndex];
            sequentialIndices[node] = (clampedIndex + 1) % node.SequenceItems.Count;
            return selectedChild;
        }

        static AiActionSequenceItem CloneSequence(AiActionSequenceItem item) {
            if (item == null) {
                return null;
            }

            var clone = new AiActionSequenceItem {
                IsRandomSequence = item.IsRandomSequence,
                Button = item.Button,
                Direction = item.Direction,
                SequenceItems = new List<AiActionSequenceItem>(),
            };

            if (item.SequenceItems == null) {
                return clone;
            }

            for (var i = 0; i < item.SequenceItems.Count; i++) {
                clone.SequenceItems.Add(CloneSequence(item.SequenceItems[i]));
            }

            return clone;
        }
    }
}