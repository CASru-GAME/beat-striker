using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Alice {
    public interface IInputFocusService {
        Observable<Unit> Changed { get; }
        Observable<bool> TextFocusChanged { get; }
        int Version { get; }
        IDisposable RegisterModal(Transform root, int playerId = -1, int priority = 0, Func<bool> isActive = null);
        IDisposable Block();
        IDisposable HoldScreen();
        IDisposable RegisterInputDevice(Transform root);
        bool CanReceive(Transform target, int playerId = -1);
        bool CanPoint(Transform target, int playerId = -1, bool requireNewFrame = false);
        IDisposable RegisterCursor(int playerId, Func<bool> click);
        bool TryClick(int playerId);
        void SetTextFocus(Transform root, bool focused);
        bool HasTextFocus { get; }
    }

    public sealed class InputFocusService : IInputFocusService, ITickable, IDisposable {
        sealed class Modal {
            public Transform Root;
            public Canvas Canvas;
            public int PlayerId, Priority, Order;
            public bool Active;
            public Func<bool> IsActive;
        }
        readonly List<Modal> modals = new();
        readonly List<Transform> devices = new();
        readonly Dictionary<int, Func<bool>> cursors = new();
        readonly Subject<Unit> changed = new();
        readonly Subject<bool> textFocusChanged = new();
        Modal top;
        Transform textRoot;
        int holds, blocks, order, version, changedFrame = -1;
        bool refreshing, disposed;
        public Observable<Unit> Changed => changed;
        public Observable<bool> TextFocusChanged => textFocusChanged;
        public int Version { get { Refresh(); return version; } }
        public bool HasTextFocus { get { Refresh(); return !disposed && textRoot && top != null && textRoot.IsChildOf(top.Root); } }
        public IDisposable RegisterModal(Transform root, int playerId = -1, int priority = 0, Func<bool> isActive = null) {
            var modal = new Modal { Root = root, Canvas = root.GetComponent<Canvas>(), PlayerId = playerId, Priority = priority, IsActive = isActive };
            modals.Add(modal);
            var raycaster = root.GetComponent<FocusGraphicRaycaster>();
            if (raycaster) raycaster.Initialize(this);
            Refresh();
            return Disposable.Create(() => { modals.Remove(modal); Refresh(); });
        }
        public IDisposable RegisterInputDevice(Transform root) {
            devices.Add(root);
            return Disposable.Create(() => devices.Remove(root));
        }
        public IDisposable HoldScreen() {
            holds++; Notify();
            return Disposable.Create(() => { holds--; Notify(); });
        }
        public IDisposable Block() {
            blocks++;
            Notify();
            return Disposable.Create(() => { blocks--; Notify(); });
        }
        public void Tick() => Refresh();
        void Refresh() {
            if (disposed || refreshing) return;
            refreshing = true;
            Modal next = null;
            bool orderingChanged = false;
            foreach (var modal in modals) {
                var active = modal.Root && modal.Root.gameObject.activeInHierarchy && (modal.IsActive == null || modal.IsActive());
                if (active && !modal.Active) modal.Order = ++order;
                orderingChanged |= modal.Active != active;
                modal.Active = active;
                if (active && (next == null || modal.Priority > next.Priority || modal.Priority == next.Priority && modal.Order > next.Order)) next = modal;
            }
            if (orderingChanged) {
                var ordered = modals.FindAll(modal => modal.Active);
                ordered.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Order.CompareTo(b.Order));
                for (var i = 0; i < ordered.Count; i++) if (ordered[i].Canvas) { ordered[i].Canvas.overrideSorting = true; ordered[i].Canvas.sortingOrder = 4000 + i * 2; }
            }
            var previous = top;
            top = next;
            refreshing = false;
            if (previous != next) Notify();
        }
        void Notify() {
            if (disposed) return;
            version++;
            changedFrame = Time.frameCount;
            if (textRoot && (top == null || !textRoot.IsChildOf(top.Root))) { textRoot = null; textFocusChanged.OnNext(false); }
            changed.OnNext(Unit.Default);
        }
        public bool CanPoint(Transform target, int playerId = -1, bool requireNewFrame = false) {
            Refresh();
            if (disposed || blocks > 0 || !target || requireNewFrame && Time.frameCount <= changedFrame) return false;
            if (playerId < 0) foreach (var device in devices) if (device && target.IsChildOf(device)) return true;
            return top == null ? holds == 0 : (target.IsChildOf(top.Root) && (playerId < 0 || top.PlayerId < 0 || playerId == top.PlayerId));
        }
        public bool CanReceive(Transform target, int playerId = -1) {
            Refresh();
            if (disposed || blocks > 0 || Time.frameCount <= changedFrame) return false;
            return top == null ? target == null && holds == 0 : target && CanPoint(target, playerId);
        }
        public IDisposable RegisterCursor(int playerId, Func<bool> click) {
            cursors.Add(playerId, click);
            return Disposable.Create(() => cursors.Remove(playerId));
        }
        public bool TryClick(int playerId) {
            Refresh();
            return CanReceive(top?.Root, playerId) && cursors.TryGetValue(playerId, out var click) && click();
        }
        public void SetTextFocus(Transform root, bool focused) {
            if (disposed) return;
            if (focused && textRoot != root && CanPoint(root)) { textRoot = root; textFocusChanged.OnNext(true); }
            else if (!focused && textRoot == root) { textRoot = null; textFocusChanged.OnNext(false); }
        }
        public void Dispose() {
            if (disposed) return;
            disposed = true; textRoot = null; top = null;
            modals.Clear(); devices.Clear(); cursors.Clear(); changed.Dispose(); textFocusChanged.Dispose();
        }
    }
}
