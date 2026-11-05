using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;

namespace Alice {
    public class RankingGradeSectionView : MonoBehaviour {
        [SerializeField] int grade;
        [SerializeField] TMP_Text heading;
        [SerializeField] GameObject emptyLabel;
        [SerializeField] Transform rowsParent;
        [SerializeField] RankingPlayerRowView rowPrefab;
        [SerializeField] ActionEmitter moreEmitter;
        readonly List<RankingPlayerRowView> rows = new();
        readonly Subject<int> moreRequested = new();
        public Observable<int> MoreRequested => moreRequested;
        public int Grade => grade;
        void Awake() { moreEmitter.OnClickEvent.Subscribe(_ => moreRequested.OnNext(grade)).AddTo(this); }
        public void Set(IReadOnlyList<RankingPlayerEntry> entries, bool more, System.Action<string> selected) {
            heading.text = PlayerGrades.Label((PlayerGrade)grade);
            while (rows.Count < entries.Count) rows.Add(Instantiate(rowPrefab, rowsParent));
            for (var i = 0; i < rows.Count; i++) {
                rows[i].gameObject.SetActive(i < entries.Count);
                if (i < entries.Count) rows[i].Set(entries[i], selected);
            }
            emptyLabel.SetActive(entries.Count == 0); moreEmitter.gameObject.SetActive(more);
        }
    }
}
