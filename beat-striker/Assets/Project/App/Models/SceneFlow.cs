using System;
using System.Collections.Generic;
using Core;
using R3;

namespace App {
    public interface ISceneFlow {
        SceneContext GetSceneContext();

        // 遷移前半が終了したことを知らせる
        TransitionOutKey GenerateTransitionInKey(SceneContext context);
        void NotifyTransitionInEnd(TransitionOutKey key);

        // 遷移後半が終了したことを知らせる
        TransitionInKey GenerateTransitionOutKey(SceneContext context);
        void NotifyTransitionOutEnd(TransitionInKey key);

        // シーン遷移を依頼
        bool RequestTransition(SceneTransitionRequest config);

        // 遷移が開始した時に呼ばれる。遷移アニメの開始処理を実行したい時などに使う
        Observable<TransitionOutStartedContext> OnTransitionOutStarted { get; }

        // 遷移中にシーンがロードされた時に呼ばれる。終盤の遷移アニメを実行したい時などに使う
        Observable<TransitionInStartedContext> OnTransitionInStarted { get; }

        // 遷移が終了した時に呼ばれる。シーン開始処理を実行したい時などに使う
        Observable<SceneStartedContext> OnSceneStarted { get; }
    }

    public record TransitionOutStartedContext(SceneContext CurrentSceneContext);
    public record TransitionInStartedContext(SceneContext OldSceneContext);
    public record SceneStartedContext(SceneContext OldSceneContext);

    public interface ISceneLoader {
        void LoadScene(SceneTransitionRequest sceneRequest, Action onComplete);
    }

    public class TransitionOutKey {
        public SceneContext Context { get; }
        public TransitionOutKey(SceneContext context) {
            Context = context;
        }
    }

    public class TransitionInKey {
        public SceneContext Context { get; }
        public TransitionInKey(SceneContext context) {
            Context = context;
        }
    }

    public class SceneContext {
    }

    public record SceneTransitionRequest(string SceneName);

    public class SceneFlow : ISceneFlow {
        SceneTransitionRequest currentSceneRequest;
        SceneContext currentContext = new();
        SceneContext transitionContext;
        bool isSceneLoaded = false;
        readonly Subject<TransitionOutStartedContext> onTransitionOutStarted = new();
        readonly Subject<TransitionInStartedContext> onTransitionInStarted = new();
        readonly Subject<SceneStartedContext> onSceneStarted = new();
        readonly List<TransitionOutKey> sceneLoadKeys = new();
        readonly List<TransitionInKey> transitionEndKeys = new();
        readonly ISceneLoader sceneLoader;

        public Observable<TransitionOutStartedContext> OnTransitionOutStarted => onTransitionOutStarted;
        public Observable<TransitionInStartedContext> OnTransitionInStarted => onTransitionInStarted;
        public Observable<SceneStartedContext> OnSceneStarted => onSceneStarted;

        public SceneFlow(ISceneLoader sceneLoader) {
            this.sceneLoader = sceneLoader;
        }

        public SceneContext GetSceneContext() => currentContext;

        public TransitionOutKey GenerateTransitionInKey(SceneContext context) {
            var key = new TransitionOutKey(context);
            sceneLoadKeys.Add(key);
            return key;
        }

        public void NotifyTransitionInEnd(TransitionOutKey key) {
            sceneLoadKeys.Remove(key);
            TryLoadScene();
        }

        public TransitionInKey GenerateTransitionOutKey(SceneContext context) {
            var key = new TransitionInKey(context);
            transitionEndKeys.Add(key);
            return key;
        }

        public void NotifyTransitionOutEnd(TransitionInKey key) {
            transitionEndKeys.Remove(key);
            TryEndTransition();
        }

        public bool RequestTransition(SceneTransitionRequest config) {
            if (currentSceneRequest != null) return false;
            currentSceneRequest = config;
            transitionContext = currentContext;
            onTransitionOutStarted.OnNext(new TransitionOutStartedContext(transitionContext));
            TryLoadScene();
            return true;
        }

        void TryLoadScene() {
            if (currentSceneRequest == null || CountKeysForContext(sceneLoadKeys, transitionContext) != 0) return;
            sceneLoader.LoadScene(currentSceneRequest, OnLoadSceneComplete);
        }

        void OnLoadSceneComplete() {
            isSceneLoaded = true;
            currentContext = new SceneContext();
            onTransitionInStarted.OnNext(new TransitionInStartedContext(transitionContext));
            TryEndTransition();
        }

        void TryEndTransition() {
            if (!isSceneLoaded || CountKeysForContext(transitionEndKeys, transitionContext) != 0) return;
            onSceneStarted.OnNext(new SceneStartedContext(transitionContext));
            currentSceneRequest = null;
            isSceneLoaded = false;
        }

        int CountKeysForContext<T>(List<T> keys, SceneContext context) where T : class {
            int count = 0;
            foreach (var key in keys) {
                var keyContext = key switch {
                    TransitionOutKey slk => slk.Context,
                    TransitionInKey tek => tek.Context,
                    _ => null
                };
                if (keyContext == context) count++;
            }
            return count;
        }
    }
}