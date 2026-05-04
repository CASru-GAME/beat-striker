using Alice;
using NUnit.Framework;
using UnityEngine;

namespace Alice.Project.Tests.EditMode {
    public class BeatOnlineCommandBufferTests {
        [Test]
        public void TrySubmit_KeepsFirstNotificationForSameBeatAndPlayer() {
            var buffer = new BeatOnlineCommandBuffer();
            var first = CreateNotification(0, 0, OnlineBeatNotificationKind.Command, GamePadButton.East);
            var second = CreateNotification(0, 0, OnlineBeatNotificationKind.Pass, default);

            Assert.That(buffer.TrySubmit(first), Is.True);
            Assert.That(buffer.TrySubmit(second), Is.False);
            Assert.That(buffer.TryGetNotification(0, 0, out var actual), Is.True);
            Assert.That(actual.Kind, Is.EqualTo(OnlineBeatNotificationKind.Command));
            Assert.That(actual.Button, Is.EqualTo(GamePadButton.East));
        }

        [Test]
        public void IsReady_ReturnsTrueWhenBothPlayersSubmittedCommandOrPass() {
            var buffer = new BeatOnlineCommandBuffer();

            buffer.TrySubmit(CreateNotification(3, 0, OnlineBeatNotificationKind.Command, GamePadButton.South));
            buffer.TrySubmit(CreateNotification(3, 1, OnlineBeatNotificationKind.Pass, default));

            Assert.That(buffer.IsReady(3, 2), Is.True);
        }

        [Test]
        public void TrySubmit_RejectsClosedBeat() {
            var buffer = new BeatOnlineCommandBuffer();

            buffer.CloseBeat(2);

            Assert.That(buffer.TrySubmit(CreateNotification(2, 0, OnlineBeatNotificationKind.Pass, default)), Is.False);
        }

        [Test]
        public void HasSubmissionAfter_DetectsFutureNotificationForPlayer() {
            var buffer = new BeatOnlineCommandBuffer();

            buffer.TrySubmit(CreateNotification(5, 1, OnlineBeatNotificationKind.Pass, default));

            Assert.That(buffer.HasSubmissionAfter(3, 1), Is.True);
            Assert.That(buffer.HasSubmissionAfter(5, 1), Is.False);
            Assert.That(buffer.HasSubmissionAfter(3, 0), Is.False);
        }

        [Test]
        public void FillMissingSubmissions_AddsPassesUntilIncomingBeat() {
            var buffer = new BeatOnlineCommandBuffer();

            var count = buffer.FillMissingSubmissions(
                1,
                2,
                5,
                beatIndex => CreateNotification(beatIndex, 1, OnlineBeatNotificationKind.Pass, default));

            Assert.That(count, Is.EqualTo(3));
            Assert.That(buffer.TryGetNotification(2, 1, out var beat2), Is.True);
            Assert.That(buffer.TryGetNotification(3, 1, out var beat3), Is.True);
            Assert.That(buffer.TryGetNotification(4, 1, out var beat4), Is.True);
            Assert.That(beat2.Kind, Is.EqualTo(OnlineBeatNotificationKind.Pass));
            Assert.That(beat3.Kind, Is.EqualTo(OnlineBeatNotificationKind.Pass));
            Assert.That(beat4.Kind, Is.EqualTo(OnlineBeatNotificationKind.Pass));
            Assert.That(buffer.HasSubmission(5, 1), Is.False);
        }

        static OnlineBeatNotificationSnapshot CreateNotification(int beatIndex, int playerId, OnlineBeatNotificationKind kind, GamePadButton button) {
            return new OnlineBeatNotificationSnapshot(
                0,
                playerId,
                beatIndex,
                beatIndex,
                kind,
                kind == OnlineBeatNotificationKind.Command ? BeatJudgeZone.Good : BeatJudgeZone.Miss,
                button,
                Vector2.zero);
        }
    }
}
