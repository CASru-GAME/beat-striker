


using System.Collections.Generic;
using App;
using UnityEngine;

namespace Alice {

    public interface IStrikerRegistry {
        Option<AliceStrikerHub> Get(int playerId);
        void RequestRegister(int playerId, AliceStrikerHub hub);
        void RequestUnregister(int playerId);
    }

    public class StrikerRegistry : IStrikerRegistry {
        readonly Dictionary<int, AliceStrikerHub> strikerHubs = new Dictionary<int, AliceStrikerHub>();

        public Option<AliceStrikerHub> Get(int playerId) {
            if (strikerHubs.TryGetValue(playerId, out var hub)) {
                return hub;
            }
            return null;
        }

        public void RequestRegister(int playerId, AliceStrikerHub hub) {
            if (!strikerHubs.ContainsKey(playerId)) {
                strikerHubs.Add(playerId, hub);
            }
        }

        public void RequestUnregister(int playerId) {
            if (strikerHubs.ContainsKey(playerId)) {
                strikerHubs.Remove(playerId);
            }
        }
    }
}