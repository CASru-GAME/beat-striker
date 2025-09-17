

using UnityEngine;

public static class Ex {
    public static Vector3 X(this Vector3 self, float v) {
        self.x = v;
        return self;
    }
    public static Vector3 Y(this Vector3 self, float v) {
        self.y = v;
        return self;
    }
    public static Vector3 Z(this Vector3 self, float v) {
        self.z = v;
        return self;
    }
}