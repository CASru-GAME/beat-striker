using UnityEngine;

namespace Core {
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class InfoBoxAttribute : PropertyAttribute {
        public readonly string message;

        public InfoBoxAttribute(string message) {
            this.message = message;
        }
    }
}