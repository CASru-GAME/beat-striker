using UnityEngine;

public class InfoBoxAttribute : PropertyAttribute {
    public readonly string message;

    public InfoBoxAttribute(string message) {
        this.message = message;
    }
}