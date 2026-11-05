using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace Alice {
    // Native pointers and the gamepad cursors use the same raycast boundary.
    public sealed class FocusGraphicRaycaster : GraphicRaycaster {
        IInputFocusService focus;
        [Inject] public void Initialize(IInputFocusService focus) => this.focus = focus;
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList) {
            // An unbound raycaster must never expose controls outside the input boundary.
            if (focus == null) return;
            var first = resultAppendList.Count;
            base.Raycast(eventData, resultAppendList);
            for (var i = resultAppendList.Count - 1; i >= first; i--) {
                if (!focus.CanPoint(resultAppendList[i].gameObject.transform,
                    eventData is CursorPointerEventData cursor ? cursor.PlayerId : -1, requireNewFrame: true)) resultAppendList.RemoveAt(i);
            }
        }
    }

    public sealed class CursorPointerEventData : PointerEventData {
        public int PlayerId { get; }
        public CursorPointerEventData(EventSystem eventSystem, int playerId) : base(eventSystem) {
            PlayerId = playerId;
            pointerId = playerId;
        }
    }
}
