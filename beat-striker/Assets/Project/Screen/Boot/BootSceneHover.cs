using Core;
using R3;
using UnityEngine;

namespace Alice {
    public class BootSceneHover : MonoBehaviour {

        [SerializeField] Botan botan;
        [SerializeField] private GameObject activeTargete; 

        void Awake() {
            activeTargete.SetActive(false);
            botan.OnHoverEvent.Subscribe(_ => {
                activeTargete.SetActive(true);
            });
            botan.OnHoverExitEvent.Subscribe(_ => {
                activeTargete.SetActive(false);
            });
        }
    }
}
