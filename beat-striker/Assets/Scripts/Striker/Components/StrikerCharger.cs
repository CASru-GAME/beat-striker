using UnityEngine;

namespace Core.Striker.Components
{
    [AddComponentMenu(" StrikerComponents/Charger", 0)]
    public class StrikerCharger : MonoBehaviour
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
