using Alice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class TutorialLessonProgressTests {
    static TutorialActionLesson Lesson(string prefab, int index) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Project/Strikers/{prefab}.prefab")
            .GetComponent<StrikerHub>().TutorialLessons[index];

    static TutorialStateExecution Execution(TutorialActionStep step, int id, bool exited,
        bool committed = true, bool shieldLaunched = false) {
        var direction = step.Direction switch {
            TutorialDirection.Up => Vector2.up,
            TutorialDirection.Down => Vector2.down,
            TutorialDirection.Forward => Vector2.right,
            _ => Vector2.zero,
        };
        var command = new TutorialCommandContext(id, step.Command, direction, step.Ground != TutorialGround.Airborne);
        return new TutorialStateExecution(step.TargetStates[0], id, command, exited, committed, shieldLaunched);
    }

    [Test]
    public void WarriorForwardNormalAttackUsesTheSameBasicAttack() {
        var lesson = Lesson("LargeSatan/MLSatan", 2);
        var progress = new TutorialLessonProgress(lesson, 0);
        var enter = Execution(lesson.Steps[0], 1, false) with {
            Command = new TutorialCommandContext(1, GamePadButton.East, Vector2.right, true),
        };
        Assert.That(progress.Observe(enter), Is.EqualTo(TutorialProgressResult.Ignored));
        Assert.That(progress.IsUnexpectedCommand(enter.Command), Is.False);
        Assert.That(progress.Observe(enter with { Exited = true }), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [TestCase(3)] // Jump, then air down attack.
    [TestCase(5)] // Temporary charge stance, then throw.
    public void EnteringSetupDoesNotInvalidateItsOwnCommand(int lessonIndex) {
        var lesson = Lesson("LargeSatan/MLSatan", lessonIndex);
        var progress = new TutorialLessonProgress(lesson, 0);
        var enter = Execution(lesson.Steps[0], 1, false);
        Assert.That(progress.Observe(enter), Is.EqualTo(TutorialProgressResult.Advanced));
        Assert.That(progress.StepIndex, Is.EqualTo(1));
        Assert.That(progress.IsUnexpectedCommand(enter.Command), Is.False);
    }

    [TestCase(3)]
    [TestCase(5)]
    public void ExpiredSetupCanBeStartedAgainWithOneInput(int lessonIndex) {
        var lesson = Lesson("LargeSatan/MLSatan", lessonIndex);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, false));
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        var retry = Execution(lesson.Steps[0], 2, false);
        Assert.That(progress.Observe(retry), Is.EqualTo(TutorialProgressResult.Advanced));
        Assert.That(progress.IsUnexpectedCommand(retry.Command), Is.False);
        Assert.That(progress.Observe(Execution(lesson.Steps[1], 3, true)), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [Test]
    public void ChargeMustCommitAndItsLateExitCannotCountAfterReset() {
        var lesson = Lesson("LargeWizard/Wizard1", 5);
        var progress = new TutorialLessonProgress(lesson, 0);
        var exit = Execution(lesson.Steps[0], 1, true, committed: false);
        progress.Observe(exit);
        Assert.That(progress.Count, Is.Zero);
        progress.Reset(1);
        progress.Observe(exit with { Committed = true });
        Assert.That(progress.Count, Is.Zero);
        progress.Observe(Execution(lesson.Steps[0], 2, true));
        progress.Observe(Execution(lesson.Steps[0], 2, true));
        Assert.That(progress.Count, Is.EqualTo(1), "Repeated exit notifications must not count twice.");
    }

    [Test]
    public void CommandValidationRunsAfterThePreviousChargeExitAdvancesTheStep() {
        var lesson = Lesson("LargeWizard/Wizard1", 4);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, false, committed: false));
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        var attack = Execution(lesson.Steps[1], 2, false, committed: false);
        progress.Observe(attack);
        Assert.That(progress.IsUnexpectedCommand(attack.Command), Is.False);
        Assert.That(progress.Observe(attack with { Exited = true, Committed = true }), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [Test]
    public void FourthGuardMustActuallyLaunchAShield() {
        var lesson = Lesson("LargeWizard/Wizard1", 8);
        var progress = new TutorialLessonProgress(lesson, 0);
        for (var id = 1; id < 4; id++) progress.Observe(Execution(lesson.Steps[0], id, true));
        Assert.That(progress.Observe(Execution(lesson.Steps[0], 4, true)), Is.EqualTo(TutorialProgressResult.Retry));
        Assert.That(progress.IsComplete, Is.False);
    }

    [Test]
    public void BufferedChargeRepeatMustNotEraseTheCompletedHeroSetup() {
        var lesson = Lesson("LargeHero/Hero", 5);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        var repeat = Execution(lesson.Steps[0], 2, false, committed: false);
        progress.Observe(repeat);
        Assert.That(progress.RequiresRetryAfterCommand(repeat.Command, repeat.State), Is.False);
        Assert.That(progress.StepIndex, Is.EqualTo(1));
        Assert.That(progress.Observe(Execution(lesson.Steps[1], 3, true)), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [Test]
    public void SpendingChargeOnTheWrongSpellRequiresANewSetup() {
        var lesson = Lesson("LargeWizard/Wizard1", 4);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        var wrongSpell = Execution(Lesson("LargeWizard/Wizard1", 5).Steps[1], 2, false);
        Assert.That(progress.RequiresRetryAfterCommand(wrongSpell.Command, wrongSpell.State), Is.True);
        Assert.That(progress.IsComplete, Is.False);
    }

    [Test]
    public void GuardingDoesNotSpendTheChargeNeededByTheNextStep() {
        var lesson = Lesson("LargeHero/Hero", 5);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        var guard = Execution(Lesson("LargeHero/Hero", 6).Steps[0], 2, false);
        Assert.That(progress.RequiresRetryAfterCommand(guard.Command, guard.State), Is.False);
        Assert.That(progress.Observe(Execution(lesson.Steps[1], 3, true)), Is.EqualTo(TutorialProgressResult.Complete));
    }
}
