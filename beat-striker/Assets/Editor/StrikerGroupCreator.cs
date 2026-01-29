using UnityEngine;
using UnityEditor;

namespace BS.Editor {
    public class StrikerGroupCreator : StrikerCreatorBase {
        private const string TEMPLATE_FILE_NAME = "StrikerGroupTemplate.cs.txt";

        protected override string TemplateFileName => TEMPLATE_FILE_NAME;

        [MenuItem("Assets/Create/🟢 Striker Group", false, 2)]
        private static void CreateStrikerGroup() {
            var creator = new StrikerGroupCreator();
            creator.CreateFile(TEMPLATE_FILE_NAME, "New", "Group");
        }
    }
}
