using UnityEngine;

namespace Core.LargeWizard{
    public class EnergyStorage : MonoBehaviour{
        int energy = 0;

        [SerializeField] GameObject charge1Prefab;
        [SerializeField] GameObject charge2Prefab;
        [SerializeField] GameObject charge3Prefab;
        [SerializeField] Vector3 chargeOffset = new Vector3(0, 1f, -1f);
        GameObject chargeInstance;

        public void StoreEnergy(int energy) {
            this.energy += energy;
            UpdateChargeEffect();
        }

        void UpdateChargeEffect() {
            var prefab = energy switch {
                1 => charge1Prefab,
                2 => charge2Prefab,
                >= 3 => charge3Prefab,
                _ => null
            };
            if (prefab == null) return;

            if (chargeInstance) {
                Destroy(chargeInstance);
            }
            chargeInstance = Instantiate(prefab, transform);
            chargeInstance.transform.localPosition = chargeOffset;
        }

        public int RetrieveEnergy() {
            int lastChargeCount = energy;
            energy = 0;
            if (chargeInstance) {
                Destroy(chargeInstance);
            }
            return lastChargeCount;
        }
    
  
}

}
