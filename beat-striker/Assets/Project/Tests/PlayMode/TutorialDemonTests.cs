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
        Assert.That(opponent.Striker, Is.EqualTo(Striker.TutorialDemon));

        var registry = app.GetComponent<AppStrikerRegistry>();
        var info = registry.GetByStriker(opponent.Striker);
        Assert.That(info.DisplayName, Is.EqualTo("魔王"));
        Assert.That(info.Portrait, Is.Not.Null);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Strikers/TutorialDemon/TutorialDemon.prefab");
        Assert.That(prefab, Is.Not.Null);
        var fighter = Object.Instantiate(prefab);
        // The real battle injects registry dependencies before the first frame.
        fighter.GetComponent<StrikerHub>().enabled = false;
        try {
            yield return null;
            var hub = fighter.GetComponent<StrikerHub>();
            Assert.That(hub.InspectorStriker, Is.EqualTo(Striker.TutorialDemon));
            Assert.That(hub.MaxHitPoint, Is.EqualTo(10000f));
            Assert.That(fighter.transform.Find("Mock Demon/Head"), Is.Not.Null);
            Assert.That(fighter.transform.Find("Mock Demon/Left horn"), Is.Not.Null);
            Assert.That(fighter.transform.Find("Mock Demon/Head").GetComponent<Collider>(), Is.Null);
        }
        finally {
            Object.Destroy(fighter);
        }
    }
}
#endif
