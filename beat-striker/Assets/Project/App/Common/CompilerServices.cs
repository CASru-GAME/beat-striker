

using System;

namespace System.Runtime.CompilerServices {
    public static class IsExternalInit { }
}

namespace App {
    public readonly struct Option<T> where T : class {
        private readonly T _value;

        private Option(T value) => _value = value;

        public static implicit operator Option<T>(T value)
            => value is not null ? new Option<T>(value) : new Option<T>(null);

        public bool TryGetValue(out T value) {
            value = _value;
            return _value is not null;
        }

        public T GetValue(T defaultValue) => _value ?? defaultValue;

        public U Map<U>(U defaultValue, Func<T, U> mapper) where U : class {
            if (_value is not null) {
                return mapper(_value);
            }
            return defaultValue;
        }

        public bool Equals(Option<T> other) => Equals(_value, other._value);
        public override bool Equals(object obj) => obj is Option<T> other && Equals(other);
        public override int GetHashCode() => _value?.GetHashCode() ?? 0;
        public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);
        public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);
    }

    public static class Extensions {
        public static string ToGreen(this string text) {
            return $"<color=#00ff00>{text}</color>";
        }

        public static string ToBold(this string text) {
            return $"<b>{text}</b>";
        }
    }
}