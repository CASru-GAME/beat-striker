


using System.Collections.Generic;
using App;
using Core.Striker;
using UnityEngine;

namespace Alice {

    public interface IStrikerRegistry {
        Option<StrikerHub> Get(int playerId);
        void RequestRegister(int playerId, StrikerHub hub);
        void RequestUnregister(int playerId);
    }

    public class StrikerRegistry : IStrikerRegistry {
        readonly Dictionary<int, StrikerHub> strikerHubs = new Dictionary<int, StrikerHub>();

        public Option<StrikerHub> Get(int playerId) {
            if (strikerHubs.TryGetValue(playerId, out var hub)) {
                return hub;
            }
            return null;
        }

        public void RequestRegister(int playerId, StrikerHub hub) {
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