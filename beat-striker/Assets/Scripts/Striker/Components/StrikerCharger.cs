using UnityEngine;

namespace Core.Striker.Components
{
    [AddComponentMenu(" StrikerComponents/Charger", 0)]
    public class StrikerCharger : MonoBehaviour
    {
        public bool IsCharged { get; private set; }

        public void Charge()
        {
            IsCharged = true;
        }

        public void ChargeEnd()
        {
            IsCharged = false;
        }
    }
}
