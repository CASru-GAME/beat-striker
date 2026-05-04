using UnityEngine;

namespace Alice {
    public class AppOverlayView : MonoBehaviour {
        [SerializeField] GameObject overlayRoot;
        [SerializeField] GameObject onlineIndicatorRoot;

        public void SetOverlayVisible(bool visible) {
            overlayRoot.SetActive(visible);
        }

        public void SetOnlineIndicatorVisible(bool visible) {
            onlineIndicatorRoot.SetActive(visible);
        }
    }
}
