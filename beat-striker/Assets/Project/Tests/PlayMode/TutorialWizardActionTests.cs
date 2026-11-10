#if UNITY_EDITOR
using System.Collections;
using Alice;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class TutorialWizardActionTests {
    [UnityTest]
    public IEnumerator OnlyTheActualJumpClearsTheLessonWhenGroundContactIsMissing() {
        var prefab = AssetDatabase.LoadAssetAtPath<StrikerHub>("Assets/Project/Strikers/LargeWizard/Wizard1.prefab");
        Assert.That(prefab, Is.Not.Null);
        var camera = new GameObject("Tutorial test camera", typeof(Camera)) { tag = "MainCamera" };
        var wizard = Object.Instantiate(prefab);
        // Drive the real command and state machine directly with no floor or scene input.
        wizard.enabled = false;

        try {
            var runtime = (AliceStrikerHub)wizard.EnsureAliceRuntimeHub();
            var registry = new StrikerRegistry();
            runtime.InitializeRuntimeDependencies(registry, null, observeTutorialCommands: true);
            registry.RequestRegister(0, runtime);
            runtime.Default();

            var lesson = runtime.TutorialLessons[0];
            var progress = new TutorialLessonProgress(lesson, runtime.TutorialCommandId);
            var jumpState = lesson.Steps[0].TargetStates[0];
            using var subscription = jumpState.OnTutorialExecution.Subscribe(execution => progress.Observe(execution));

            runtime.ChangeDirection(Vector2.right);
            runtime.Dash();
            Assert.That(runtime.ObservedState, Is.TypeOf<Core.LargeWizard.DashState>());
            Assert.That(progress.IsComplete, Is.False);

            runtime.Default();
            Assert.That(runtime.IsTutorialGrounded, Is.False);
            runtime.ChangeDirection(new Vector2(0.9f, 0.4f));
            runtime.Dash();
            Assert.That(runtime.ObservedState, Is.SameAs(jumpState));
            Assert.That(wizard.Rigidbody.linearVelocity.y, Is.GreaterThan(0f));
            Assert.That(progress.IsComplete, Is.True);
        }
        finally {
            Object.Destroy(wizard.gameObject);
            Object.Destroy(camera);
        }
        yield return null;
    }
}
#endif
