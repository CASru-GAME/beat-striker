using UnityEngine;

namespace Core.Striker.Darling.Components
{
    [AddComponentMenu(" StrikerComponents/Charger", 0)]
    public class DarlingCharger : MonoBehaviour
    {
        public int Count { get; private set; }

        public void Charge()
        {
            Count++;
        }

        public void ChargeEnd()
        {
            Count = 0;
        }
    }
}
