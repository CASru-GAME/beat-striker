#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Alice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class TutorialDemonTests {
    [UnityTest]
    public IEnumerator TutorialOpponentUsesHighHpDemonPrefab() {
        var app = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/App/App.prefab");
        var setting = app.GetComponent<TutorialSetting>();
        var opponent = setting.BattleSelection.PlayerSelections.Single(selection => selection.PlayerId == 1);
        Assert.That(opponent.Striker, Is.EqualTo(Striker.DemonKing));

        var registry = app.GetComponent<AppStrikerRegistry>();
        var info = registry.GetByStriker(opponent.Striker);
        Assert.That(info.DisplayName, Is.EqualTo("魔王"));
        Assert.That(info.Portrait, Is.Not.Null);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Strikers/DarkSatan/DemonKing.prefab");
        Assert.That(prefab, Is.Not.Null);
        var fighter = Object.Instantiate(prefab);
        // The real battle injects registry dependencies before the first frame.
        fighter.GetComponent<StrikerHub>().enabled = false;
        try {
            yield return null;
            var hub = fighter.GetComponent<StrikerHub>();
            Assert.That(hub.InspectorStriker, Is.EqualTo(Striker.DemonKing));
            Assert.That(hub.MaxHitPoint, Is.EqualTo(500f));
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
}
#endif
