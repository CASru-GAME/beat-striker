using Alice;
using NUnit.Framework;
using UnityEngine;

namespace Alice.Project.Tests.EditMode {
    public class BattleHistoryReplayTests {
        [Test]
        public void ReplaySetting_StoresAndClearsPayload() {
            var setting = new ReplaySetting();
            var payload = new ReplayPayload {
                stage = Stage.Live.ToString(),
                musicId = "music",
                strikerIds = new[] { (int)Striker.Hero, (int)Striker.Wizard },
                appVersion = "test",
            };

            setting.SetReplay(payload);

            Assert.That(setting.HasReplay, Is.True);
            Assert.That(setting.TryGetReplay(out var actual), Is.True);
            Assert.That(actual.musicId, Is.EqualTo("music"));

            setting.ClearReplay();

            Assert.That(setting.HasReplay, Is.False);
            Assert.That(setting.TryGetReplay(out _), Is.False);
        }

        [Test]
        public void ReplayPayload_RoundTripsThroughJsonUtility() {
            var payload = new ReplayPayload {
                schemaVersion = 1,
                stage = Stage.Street.ToString(),
                musicId = "song",
                strikerIds = new[] { (int)Striker.Fighter, (int)Striker.Warrior },
                appVersion = "0.1.0",
                rounds = new[] {
                    new ReplayRoundPayload {
                        roundNumber = 1,
                        beatNotifications = new[] {
                            new ReplayBeatNotificationPayload {
                                playerId = 0,
                                beatIndex = 3,
                                time = 1.25f,
                                kind = (int)OnlineBeatNotificationKind.Command,
                                zone = (int)BeatJudgeZone.Good,
                                button = (int)GamePadButton.East,
                                directionX = 1f,
                                directionY = 0f,
                            }
                        },
                        preBeatStates = new[] {
                            new ReplayPreBeatStatePayload {
                                playerId = 1,
                                applyBeatIndex = 3,
                                hitPoint = 80f,
                                specialPoint = 20f,
                                position = new Vector3(1f, 2f, 3f),
                                statePathId = "Root/Idle",
                                playbackTime = 1.2f,
                            }
                        }
                    }
                }
            };

            var json = JsonUtility.ToJson(payload);
            var actual = JsonUtility.FromJson<ReplayPayload>(json);

            Assert.That(actual.stage, Is.EqualTo(Stage.Street.ToString()));
            Assert.That(actual.rounds.Length, Is.EqualTo(1));
            Assert.That(actual.rounds[0].beatNotifications[0].button, Is.EqualTo((int)GamePadButton.East));
            Assert.That(actual.rounds[0].preBeatStates[0].position.x, Is.EqualTo(1f));
        }
    }
}
