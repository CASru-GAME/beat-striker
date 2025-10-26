
using Core.EventBus;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Utils {
    public sealed class FakeBus : IBus {
        public readonly List<object> Messages = new List<object>();

        public void Publish<T>(T message) {
            Messages.Add(message!);
        }

        public IEnumerable<T> OfType<T>() => Messages.OfType<T>();

        public T GetMessage<T>() {
            Assert.That(Messages.Count, Is.GreaterThan(0), "Messages list is empty");
            var last = Messages.OfType<T>().LastOrDefault();
            Assert.That(last, Is.Not.Null, $"No message of type {typeof(T)} found");
            return last!;
        }   

        public void Clear() => Messages.Clear();

        public void Subscribe<T>(Action<T> handler) {
            throw new NotImplementedException();
        }

        public void Unsubscribe<T>(Action<T> handler) {
            throw new NotImplementedException();
        }
    }
}