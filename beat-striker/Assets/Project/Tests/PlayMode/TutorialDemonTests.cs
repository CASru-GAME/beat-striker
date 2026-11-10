#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Alice;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class TutorialDemonTests {
    const string DemonPrefabPath = "Assets/Project/Strikers/DarkSatan/DemonKing.prefab";

    [UnityTest]
    public IEnumerator TutorialOpponentUsesDemonKingPrefab() {
        var app = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/App/App.prefab");
        var setting = app.GetComponent<TutorialSetting>();
        var opponent = setting.BattleSelection.PlayerSelections.Single(selection => selection.PlayerId == 1);
        Assert.That(opponent.Striker, Is.EqualTo(Striker.DemonKing));

        var registry = app.GetComponent<AppStrikerRegistry>();
        var info = registry.GetByStriker(opponent.Striker);
        Assert.That(info.DisplayName, Is.EqualTo("魔王"));
        Assert.That(info.Portrait, Is.Not.Null);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemonPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var fighter = Object.Instantiate(prefab);
        // The real battle injects registry dependencies before the first frame.
        fighter.GetComponent<StrikerHub>().enabled = false;
        try {
            yield return null;
            var hub = fighter.GetComponent<StrikerHub>();
            Assert.That(hub.InspectorStriker, Is.EqualTo(Striker.DemonKing));
            var visual = fighter.transform.Find("DemonKing Visual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(fighter.GetComponent<DemonKingVisual>(), Is.Null);
            var animator = visual.GetComponent<Animator>();
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.humanScale, Is.InRange(.5f, 3f), "Humanoid units must match the fighter's gameplay scale.");
            Assert.That(visual.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.GreaterThan(0));
            Assert.That(fighter.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(component => component != null)
                .Any(component => component.GetType().Namespace == "Core.LargeSatan"), Is.False);
        }
        finally {
            Object.Destroy(fighter);
        }
    }

    [UnityTest]
    public IEnumerator TutorialOpponentEmitsDefeatOnceAfterSurvivalIsDisabled() {
        var prefab = AssetDatabase.LoadAssetAtPath<StrikerHub>(DemonPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var demon = Object.Instantiate(prefab);
        demon.enabled = false;
        try {
            var runtime = (AliceStrikerHub)demon.EnsureAliceRuntimeHub();
            runtime.InitializeRuntimeDependencies(new StrikerRegistry(), null, observeTutorialCommands: false);
            runtime.Default();

            var defeats = 0;
            using var subscription = runtime.OnDead.Subscribe(_ => defeats++);
            runtime.ConfigureSurvival(true, 0f);
            runtime.ApplyDamage(runtime.MaxHitPoint.CurrentValue);
            Assert.That(defeats, Is.Zero);

            runtime.RestoreHitPoint();
            runtime.ConfigureSurvival(false, 0f);
            Assert.That(runtime.HitPoint.CurrentValue, Is.EqualTo(runtime.MaxHitPoint.CurrentValue));
            runtime.ApplyDamage(runtime.MaxHitPoint.CurrentValue);
            runtime.ApplyDamage(1f);
            Assert.That(defeats, Is.EqualTo(1));
            Assert.That(runtime.ObservedState, Is.TypeOf<Core.DarkSatan.DemonDeathState>());
        }
        finally {
            Object.Destroy(demon.gameObject);
        }
        yield return null;
    }
}
#endif
