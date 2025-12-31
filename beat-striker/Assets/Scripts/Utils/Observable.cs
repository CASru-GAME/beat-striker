using System;
using System.Collections.Generic;

namespace Core.Utils {

    /// <summary>
    /// サブスクリプション解除用のディスポーザブル
    /// </summary>
    public sealed class Subscription : IDisposable {
        private Action unsubscribe;

        public Subscription(Action unsubscribe) {
            this.unsubscribe = unsubscribe;
        }

        public void Dispose() {
            unsubscribe?.Invoke();
            unsubscribe = null;
        }
    }

    /// <summary>
    /// 複数のサブスクリプションをまとめて管理するディスポーザブル
    /// </summary>
    public sealed class CompositeDisposable : IDisposable {
        private readonly List<IDisposable> disposables = new();
        private bool isDisposed = false;

        public void Add(IDisposable disposable) {
            if (isDisposed) {
                disposable?.Dispose();
                return;
            }
            disposables.Add(disposable);
        }

        public void Dispose() {
            if (isDisposed) return;
            isDisposed = true;
            foreach (var d in disposables) {
                d?.Dispose();
            }
            disposables.Clear();
        }
    }

    /// <summary>
    /// オブザーバブルなプロパティ。値が変更されると登録されたリスナーに通知する。
    /// </summary>
    public sealed class Observable<T> {
        private T value;
        private event Action<T> onChanged;

        public T Value {
            get => value;
            set {
                if (!EqualityComparer<T>.Default.Equals(this.value, value)) {
                    this.value = value;
                    onChanged?.Invoke(value);
                }
            }
        }

        public Observable(T initialValue = default) {
            value = initialValue;
        }

        public IDisposable Subscribe(Action<T> listener) {
            onChanged += listener;
            return new Subscription(() => onChanged -= listener);
        }

        /// <summary>
        /// 現在の値で即座にリスナーを呼び出し、その後の変更も通知する
        /// </summary>
        public IDisposable SubscribeWithCurrent(Action<T> listener) {
            listener?.Invoke(value);
            return Subscribe(listener);
        }
    }

    /// <summary>
    /// イベント発火専用のオブザーバブル（値を持たない）
    /// </summary>
    public sealed class Subject {
        private event Action onNext;

        public void Fire() {
            onNext?.Invoke();
        }

        public IDisposable Subscribe(Action listener) {
            onNext += listener;
            return new Subscription(() => onNext -= listener);
        }
    }

    /// <summary>
    /// 値を持つイベント発火用のオブザーバブル
    /// </summary>
    public sealed class Subject<T> {
        private event Action<T> onNext;

        public void Fire(T value) {
            onNext?.Invoke(value);
        }

        public IDisposable Subscribe(Action<T> listener) {
            onNext += listener;
            return new Subscription(() => onNext -= listener);
        }
    }
}
