using UnityEngine;

namespace Core.DarkSatan {
    public sealed class DemonNoteBulletVisual : MonoBehaviour {
        [SerializeField] GameObject[] symbols;

        public void SelectRandom() {
            int selected = Random.Range(0, symbols.Length);
            for (int i = 0; i < symbols.Length; i++) symbols[i].SetActive(i == selected);
        }
    }
}
