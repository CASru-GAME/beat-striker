using System;
using System.Collections.Generic;

namespace App {
    public interface ObservableFunc<TArg, TResult> {
        IDisposable Subscribe(Func<TArg, TResult> handler);
    }

    public sealed class FuncSubject<TArg, TResult> : ObservableFunc<TArg, TResult> {
        private readonly List<Func<TArg, TResult>> handlers = new();

        public IDisposable Subscribe(Func<TArg, TResult> handler) {
            handlers.Add(handler);
            return new Subscription(this, handler);
        }

        public TResult[] InvokeAll(TArg arg) {
            var snapshot = handlers.ToArray();
            var results = new TResult[snapshot.Length];
            for (var i = 0; i < snapshot.Length; i++) {
                results[i] = snapshot[i](arg);
            }
            return results;
        }

        private sealed class Subscription : IDisposable {
            private readonly FuncSubject<TArg, TResult> parent;
            private readonly Func<TArg, TResult> handler;

            public Subscription(FuncSubject<TArg, TResult> parent, Func<TArg, TResult> handler) {
                this.parent = parent;
                this.handler = handler;
            }

            public void Dispose() {
                parent.handlers.Remove(handler);
            }
        }
    }

    public static class FuncSubjectExtensions {
        public static bool InvokeAllAnd<TArg>(this FuncSubject<TArg, bool> subject, TArg arg) {
            var results = subject.InvokeAll(arg);
            for (var i = 0; i < results.Length; i++) {
                if (!results[i]) {
                    return false;
                }
            }
            return true;
        }
    }
}
