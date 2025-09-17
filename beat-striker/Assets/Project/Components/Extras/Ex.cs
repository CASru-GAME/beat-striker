

using System;
using System.Linq;
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

public class EventWrapper<T> {
    public event Action<T> Handler;

    public void Add(Action<T> handler) => Handler += handler;
    public void Add(Action handler) => Handler += _ => handler();

    public void Invoke(T arg) => Handler?.Invoke(arg);
}

public class EventWrapper {
    private event Action handlers;
    public void Add(Action h) => handlers += h;
    public void Invoke() => handlers?.Invoke();
}

