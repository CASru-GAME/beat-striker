using UnityEngine;

namespace Core.LargeHero {
    public class EnergyStorage : MonoBehaviour, Alice.ITutorialResourceReset {
        public Alice.TutorialReset Resource => Alice.TutorialReset.Charge;
        public void ResetTutorialResource() => energy = 0;
        int energy = 0;
        public int StoredEnergy => energy;
        public void StoreEnergy(int energy) {
            this.energy += energy;
        }

      

        public int RetrieveEnergy() {
            
        
            int lastChargeCount = energy;
            energy = 0;
            return lastChargeCount;
        }
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    }
}
