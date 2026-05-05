using System;
using Alice;
using NUnit.Framework;

namespace Alice.Project.Tests.EditMode {
    public class OnlineMatchProtocolTests {
        [Test]
        public void RequestRoundTrip_PreservesReservationFields() {
            var request = new OnlineMatchRequest(
                Striker.Warrior,
                Stage.Street,
                "music-a",
                "reservation-1",
                "session-1");

            var parsed = OnlineMatchProtocol.DeserializeRequest(new ArraySegment<byte>(OnlineMatchProtocol.SerializeRequest(request)));

            Assert.That(parsed.LocalStriker, Is.EqualTo(request.LocalStriker));
            Assert.That(parsed.CandidateStage, Is.EqualTo(request.CandidateStage));
            Assert.That(parsed.CandidateMusicId, Is.EqualTo(request.CandidateMusicId));
            Assert.That(parsed.ReservationId, Is.EqualTo("reservation-1"));
            Assert.That(parsed.DuelSessionId, Is.EqualTo("session-1"));
        }

        [Test]
        public void RequestRoundTrip_DefaultsReservationFieldsToEmpty() {
            var request = new OnlineMatchRequest(Striker.Wizard, Stage.Live, "music-b");

            var parsed = OnlineMatchProtocol.DeserializeRequest(new ArraySegment<byte>(OnlineMatchProtocol.SerializeRequest(request)));

            Assert.That(parsed.ReservationId, Is.EqualTo(""));
            Assert.That(parsed.DuelSessionId, Is.EqualTo(""));
        }
    }
}
