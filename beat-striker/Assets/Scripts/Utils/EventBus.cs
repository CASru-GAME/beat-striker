using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Utils {

    public interface IBus {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T evt);
    }

    public sealed class Bus : IBus {
        private readonly Dictionary<Type, Delegate> map = new();
        public void Subscribe<T>(Action<T> handler) {
            if (map.TryGetValue(typeof(T), out var d))
                map[typeof(T)] = Delegate.Combine(d, handler);
            else
                map[typeof(T)] = handler;
        }

        public void Unsubscribe<T>(Action<T> handler) {
            if (map.TryGetValue(typeof(T), out var d)) {
                var current = Delegate.Remove(d, handler);
                if (current == null) map.Remove(typeof(T));
                else map[typeof(T)] = current;
            }
        }

        public void Publish<T>(T evt) {
            if (map.TryGetValue(typeof(T), out var d))
                (d as Action<T>)?.Invoke(evt);
        }
    }

    public static class BusExtensions {
        private static readonly IBus bus = new Bus();

        public static IBus GetBus(this Component o) {
            return bus;
        }
    }
}