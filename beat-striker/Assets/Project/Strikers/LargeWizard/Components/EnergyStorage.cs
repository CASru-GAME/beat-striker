using UnityEngine;

namespace Core.LargeWizard{
    public class EnergyStorage : MonoBehaviour{
        int energy = 0;

        public void StoreEnergy(int energy) {
            this.energy += energy;
        }

        public int RetrieveEnergy() {
            int lastChargeCount = energy;
            energy = 0;
            return lastChargeCount;
        }
    
  
}

}
