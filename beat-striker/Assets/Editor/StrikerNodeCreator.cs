using UnityEngine;
using UnityEditor;

namespace BS.Editor {
    public class StrikerNodeCreator : StrikerCreatorBase {
        private const string TEMPLATE_FILE_NAME = "StrikerNodeTemplate.cs.txt";

        protected override string TemplateFileName => TEMPLATE_FILE_NAME;

        [MenuItem("Assets/Create/🔵 Striker Node", false, 2)]
        private static void CreateStrikerNode() {
            var creator = new StrikerNodeCreator();
            creator.CreateFile(TEMPLATE_FILE_NAME, "New", "Node");
        }
    }
}
