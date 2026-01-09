using UnityEngine;
using UnityEditor;

namespace BS.Editor {
    public class StrikerStateCreator : StrikerCreatorBase {
        private const string TEMPLATE_FILE_NAME = "StrikerStateTemplate.cs.txt";

        protected override string TemplateFileName => TEMPLATE_FILE_NAME;

        [MenuItem("Assets/Create/🔴 Striker State", false, 1)]
        private static void CreateStrikerState() {
            var creator = new StrikerStateCreator();
            creator.CreateFile(TEMPLATE_FILE_NAME, "New", "State");
        }
    }
}
