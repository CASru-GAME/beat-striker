

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Strikers {
    readonly Ranker[] strikers;
    readonly Ranker[] rankers;
    int nextRank;
    public int Count => rankers.Length;

    internal Strikers(int strikerCount) {
        strikers = new Ranker[strikerCount];
        rankers = new Ranker[strikerCount];
    }

    public void Spawn(IEnumerable<StrikerPrefab> strikerPrefabs, IEnumerable<Transform> spawnTransforms) {
        for (int i = 0; i < rankers.Length; i++) {
            Player player = i >= App.Instance.players.Count ? null : App.Instance.players[i];
            if (!player) player = Object.Instantiate(App.Instance.cpuPrefab);
            Transform trans = spawnTransforms.ElementAt(i);
            var striker = Object.Instantiate(strikerPrefabs.FirstOrDefault(s => s.type == player.striker).prefab, trans.position, trans.rotation, null);
            striker.player = player;
            trans.SetParent(striker.transform);
            rankers[i] = strikers[i] = new Ranker(striker, i, 0);
        }

        nextRank = rankers.Length - 1;
    }

    public void RebindPlayers() {
        for (int i = 0; i < rankers.Length; i++) {
            Player player = i >= App.Instance.players.Count ? null : App.Instance.players[i];
            if (player) rankers[i].Striker.player = player;
        }
    }

    public bool Rank() {
        foreach (var striker in rankers) {
            if (striker.Rank <= nextRank && striker.Striker.hp <= 0) {
                striker.Rank = nextRank;
                nextRank--;
            }
        }

        System.Array.Sort(rankers, (a, b) => a.Rank - b.Rank);

        return nextRank <= 0;
    }

    public Ranker GetByRank(int rank) {
        return rankers[rank];
    }

    public IEnumerable<Ranker> SliceByRank(int rank) {
        return rankers.Skip(rank);
    }

    public Ranker Get(int strikerId) {
        return strikers[strikerId];
    }

    public class Ranker {
        public int Rank { get; internal set; }
        public int Id { get; private set; }
        public Striker Striker { get; private set; }

        public Ranker(Striker striker, int id, int rank) {
            Rank = rank;
            Striker = striker;
            Id = id;
        }

        public static implicit operator Striker(Ranker id) => id.Striker;
    }
}