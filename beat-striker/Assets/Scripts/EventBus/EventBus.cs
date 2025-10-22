using System;
using System.Collections.Generic;

namespace Core.EventBus {

    public static class Bus {
        private static readonly Dictionary<Type, Delegate> map = new();

        public static void Subscribe<T>(Action<T> handler) {
            if (map.TryGetValue(typeof(T), out var d))
                map[typeof(T)] = Delegate.Combine(d, handler);
            else
                map[typeof(T)] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler) {
            if (map.TryGetValue(typeof(T), out var d)) {
                var current = Delegate.Remove(d, handler);
                if (current == null) map.Remove(typeof(T));
                else map[typeof(T)] = current;
            }
        }

        public static void Publish<T>(T evt) {
            if (map.TryGetValue(typeof(T), out var d))
                (d as Action<T>)?.Invoke(evt);
        }
    }
}