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
        var direction = step.CueDirection switch {
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
        Assert.That(progress.Observe(enter with { Exited = true }), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [TestCase(4)] // Temporary charge stance, then throw.
    [TestCase(5)] // Jump, then air down attack.
    public void EnteringSetupDoesNotInvalidateItsOwnCommand(int lessonIndex) {
        var lesson = Lesson("LargeSatan/MLSatan", lessonIndex);
        var progress = new TutorialLessonProgress(lesson, 0);
        var enter = Execution(lesson.Steps[0], 1, false);
        Assert.That(progress.Observe(enter), Is.EqualTo(TutorialProgressResult.Advanced));
        Assert.That(progress.StepIndex, Is.EqualTo(1));
        Assert.That(progress.RequiresRetryAfterCommand(enter.Command, enter.State), Is.False);
    }

    [TestCase(4)]
    [TestCase(5)]
    public void ExpiredSetupCanBeStartedAgainWithOneInput(int lessonIndex) {
        var lesson = Lesson("LargeSatan/MLSatan", lessonIndex);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, false));
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        var retry = Execution(lesson.Steps[0], 2, false);
        Assert.That(progress.Observe(retry), Is.EqualTo(TutorialProgressResult.Advanced));
        Assert.That(progress.RequiresRetryAfterCommand(retry.Command, retry.State), Is.False);
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
        Assert.That(progress.RequiresRetryAfterCommand(attack.Command, attack.State), Is.False);
        Assert.That(progress.Observe(attack with { Exited = true, Committed = true }), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [Test]
    public void GuardPracticeKeepsProgressUntilAShieldActuallyLaunches() {
        var lesson = Lesson("LargeWizard/Wizard1", 8);
        var progress = new TutorialLessonProgress(lesson, 0);
        for (var id = 1; id <= 4; id++) progress.Observe(Execution(lesson.Steps[0], id, true));
        Assert.That(progress.IsComplete, Is.False);
        Assert.That(progress.Count, Is.EqualTo(4));
        Assert.That(progress.Observe(Execution(lesson.Steps[0], 5, true, shieldLaunched: true)),
            Is.EqualTo(TutorialProgressResult.Complete));
    }

    [Test]
    public void DirectionalShieldLaunchCountsBeforeTheFourthGuard() {
        var lesson = Lesson("LargeWizard/Wizard1", 8);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        var launch = Execution(lesson.Steps[0], 2, true, shieldLaunched: true) with {
            Command = new TutorialCommandContext(2, GamePadButton.North, Vector2.right, true),
        };
        progress.Observe(launch);
        progress.Observe(Execution(lesson.Steps[0], 3, true));
        Assert.That(progress.IsComplete, Is.False);
        Assert.That(progress.Observe(Execution(lesson.Steps[0], 4, true)), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [Test]
    public void DemonKingCanChargeAndReleaseInTheSameState() {
        var lesson = Lesson("DarkSatan/DemonKing", 4);
        Assert.That(lesson.Steps[0].TargetStates[0], Is.SameAs(lesson.Steps[1].TargetStates[0]));
        var progress = new TutorialLessonProgress(lesson, 0);
        Assert.That(progress.Observe(Execution(lesson.Steps[0], 1, false, committed: false)),
            Is.EqualTo(TutorialProgressResult.Advanced));
        Assert.That(progress.Observe(Execution(lesson.Steps[1], 2, true)),
            Is.EqualTo(TutorialProgressResult.Complete));
    }

    [Test]
    public void InterruptedSpellRestartsTheSpentChargeSetup() {
        var lesson = Lesson("LargeWizard/Wizard1", 4);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        Assert.That(progress.Observe(Execution(lesson.Steps[1], 2, true, committed: false)),
            Is.EqualTo(TutorialProgressResult.Retry));
        Assert.That(progress.IsComplete, Is.False);
    }

    [TestCase("LargeHero/Hero", 0f, 0f)]
    [TestCase("LargeHero/Hero", 0.9f, 0.4f)]
    [TestCase("LargeSatan/MLSatan", 0f, 0f)]
    [TestCase("LargeSatan/MLSatan", 0.9f, 0.4f)]
    [TestCase("DarkSatan/DemonKing", 0f, 0f)]
    [TestCase("DarkSatan/DemonKing", 0.9f, 0.4f)]
    public void NeutralAndShallowDiagonalInputsCountWhenTheyStartAJump(string prefab, float x, float y) {
        var lesson = Lesson(prefab, 0);
        var progress = new TutorialLessonProgress(lesson, 0);
        var jump = Execution(lesson.Steps[0], 1, false) with {
            Command = new TutorialCommandContext(1, GamePadButton.West, new Vector2(x, y).normalized, true),
        };
        Assert.That(lesson.Steps[0].CueDirection, Is.EqualTo(TutorialDirection.Up));
        Assert.That(progress.Observe(jump), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [Test]
    public void HeroHorizontalDashDoesNotClearTheJumpLessonEvenThoughItUsesTheSameState() {
        var jumpLesson = Lesson("LargeHero/Hero", 0);
        var forwardLesson = Lesson("LargeHero/Hero", 1);
        var dash = Execution(forwardLesson.Steps[0], 1, false);
        Assert.That(dash.State, Is.SameAs(jumpLesson.Steps[0].TargetStates[0]));
        Assert.That(new TutorialLessonProgress(jumpLesson, 0).Observe(dash), Is.EqualTo(TutorialProgressResult.Ignored));
        Assert.That(new TutorialLessonProgress(forwardLesson, 0).Observe(dash), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [TestCase("LargeWizard/Wizard1")]
    [TestCase("DarkSatan/DemonKing")]
    public void BasicAttackCountsWhenTheSameAttackExecutesInTheAir(string prefab) {
        var lesson = Lesson(prefab, 2);
        var progress = new TutorialLessonProgress(lesson, 0);
        var attack = Execution(lesson.Steps[0], 1, true) with {
            Command = new TutorialCommandContext(1, GamePadButton.East, Vector2.zero, false),
        };
        Assert.That(progress.Observe(attack), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [TestCase(0f, 1f, false)]
    [TestCase(0.9f, 0.4f, true)]
    public void WizardJumpCountsOnEntryWithAnyInputThatActuallyStartsTheJump(float x, float y, bool grounded) {
        var lesson = Lesson("LargeWizard/Wizard1", 0);
        var progress = new TutorialLessonProgress(lesson, 0);
        var jump = Execution(lesson.Steps[0], 1, false) with {
            Command = new TutorialCommandContext(1, GamePadButton.West, new Vector2(x, y).normalized, grounded),
        };
        Assert.That(lesson.Steps[0].CueDirection, Is.EqualTo(TutorialDirection.Up));
        Assert.That(progress.Observe(jump), Is.EqualTo(TutorialProgressResult.Complete));
    }

    [Test]
    public void WizardHorizontalDashDoesNotClearTheJumpLesson() {
        var lesson = Lesson("LargeWizard/Wizard1", 0);
        var dashStep = Lesson("LargeWizard/Wizard1", 1).Steps[0];
        var progress = new TutorialLessonProgress(lesson, 0);
        Assert.That(progress.Observe(Execution(dashStep, 1, false)), Is.EqualTo(TutorialProgressResult.Ignored));
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
    public void HeroBeamCountsWhenItActuallyFiresInTheAir() {
        var lesson = Lesson("LargeHero/Hero", 5);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        var beam = Execution(lesson.Steps[1], 2, true) with {
            Command = new TutorialCommandContext(2, GamePadButton.East, Vector2.zero, false),
        };
        Assert.That(progress.Observe(beam), Is.EqualTo(TutorialProgressResult.Complete));
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
    public void AttackInputWithoutAStateTransitionDoesNotDiscardStoredCharge() {
        var lesson = Lesson("LargeWizard/Wizard1", 4);
        var progress = new TutorialLessonProgress(lesson, 0);
        progress.Observe(Execution(lesson.Steps[0], 1, true));
        var ignoredInput = Execution(lesson.Steps[1], 2, false).Command;
        Assert.That(progress.RequiresRetryAfterCommand(ignoredInput, null), Is.False);
        Assert.That(progress.StepIndex, Is.EqualTo(1));
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
