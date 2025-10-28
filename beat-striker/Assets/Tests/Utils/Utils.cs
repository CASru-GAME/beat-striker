
using Core.Utils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Utils {
    public sealed class FakeLifeMutater : ILifeMutater {
        public bool isEnabled = false;

        public void SetEnable(bool isEnabled) {
            if (isEnabled) {
                this.isEnabled = true;
            } else {
                this.isEnabled = false;
            }
        }
    }


    public sealed class FakeLife : ILife {
        private Action onEnable = delegate { };
        private Action onDisable = delegate { };

        public void Enable() {
            onEnable?.Invoke();
        }
        public void Disable() {
            onDisable?.Invoke();
        }

        public void Link(Action onEnabled, Action onDisabled) {
            onEnable += onEnabled;
            onDisable += onDisabled;
        }

        public void Unlink(Action onEnabled, Action onDisabled) {
            onEnable -= onEnabled;
            onDisable -= onDisabled;
        }
    }

    public sealed class FakeBus : IBus {
        private readonly List<object> messages = new();
        public IReadOnlyList<object> Messages => messages.AsReadOnly();
        private readonly Dictionary<Type, List<Delegate>> handlers = new();

        public void Publish<T>(T message) {
            messages.Add(message!);
            if (handlers.TryGetValue(typeof(T), out var list)) {
                foreach (var handler in list) {
                    ((Action<T>)handler)(message);
                }
            }
        }

        public IEnumerable<T> OfType<T>() => Messages.OfType<T>();

        public T GetMessage<T>() {
            Assert.That(Messages.Count, Is.GreaterThan(0), "Messages list is empty");
            var last = Messages.OfType<T>().LastOrDefault();
            Assert.That(last, Is.Not.Null, $"No message of type {typeof(T)} found");
            return last!;
        }

        public void CantGetMessage<T>() {
            Assert.IsNull(Messages.OfType<T>().LastOrDefault(), $"Message of type {typeof(T)} was found but not expected");
        }

        public void ClearMessages() {
            messages.Clear();
        }
        
        public void ClearHandlers(){
            handlers.Clear();
        }

        public int CountPublished<T>() => Messages.OfType<T>().Count();

        public void Subscribe<T>(Action<T> handler) {
            var type = typeof(T);
            if (!handlers.ContainsKey(type)) {
                handlers[type] = new List<Delegate>();
            }
            handlers[type].Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) {
            var type = typeof(T);
            if (handlers.TryGetValue(type, out var list)) {
                list.Remove(handler);
            }
        }
    }
}