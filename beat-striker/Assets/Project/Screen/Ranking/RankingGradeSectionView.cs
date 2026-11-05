using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Alice {
    public class RankingGradeSectionView : MonoBehaviour {
        [SerializeField] int grade;
        [SerializeField] GradeBadgeView gradeView;
        [SerializeField] GameObject emptyLabel;
        [SerializeField] Transform rowsParent;
        [SerializeField] RankingPlayerRowView rowPrefab;
        readonly List<RankingPlayerRowView> rows = new();
        public int Grade => grade;
        public void Set(IReadOnlyList<RankingPlayerEntry> entries, System.Action<string> selected) {
            gradeView.Set(grade);
            while (rows.Count < entries.Count) rows.Add(Instantiate(rowPrefab, rowsParent));
            for (var i = 0; i < rows.Count; i++) {
                rows[i].gameObject.SetActive(i < entries.Count);
                if (i < entries.Count) rows[i].Set(entries[i], selected);
            }
            emptyLabel.SetActive(entries.Count == 0);
        }
    }
}
