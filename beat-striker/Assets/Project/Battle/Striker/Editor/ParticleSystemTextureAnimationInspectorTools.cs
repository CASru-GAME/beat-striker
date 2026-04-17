using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ParticleSystemTextureAnimationInspectorTools {
    private const string UNDO_LABEL = "Apply Texture Animation Setup";

    static ParticleSystemTextureAnimationInspectorTools() {
        Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
    }

    private static void OnFinishedDefaultHeaderGUI(Editor editor) {
        if (editor.target is not ParticleSystem) {
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Apply Texture Animation Setup", GUILayout.Width(220f))) {
            ApplyToEditorTargets(editor);
        }

        GUILayout.EndHorizontal();
    }

    [MenuItem("CONTEXT/ParticleSystem/Apply Texture Animation Setup")]
    private static void ApplyFromContextMenu(MenuCommand command) {
        var particleSystem = (ParticleSystem)command.context;
        ApplyToHierarchyWithUndo(particleSystem);
    }

    [MenuItem("CONTEXT/ParticleSystem/Apply Texture Animation Setup", true)]
    private static bool ValidateApplyFromContextMenu(MenuCommand command) {
        return command.context is ParticleSystem;
    }

    private static void ApplyToEditorTargets(Editor editor) {
        for (var i = 0; i < editor.targets.Length; i++) {
            var particleSystem = (ParticleSystem)editor.targets[i];
            ApplyToHierarchyWithUndo(particleSystem);
        }
    }

    private static void ApplyToHierarchyWithUndo(ParticleSystem particleSystem) {
        var hierarchyParticleSystems = particleSystem.GetComponentsInChildren<ParticleSystem>(true);

        for (var j = 0; j < hierarchyParticleSystems.Length; j++) {
            var targetParticleSystem = hierarchyParticleSystems[j];
            Undo.RecordObject(targetParticleSystem, UNDO_LABEL);

            var renderer = targetParticleSystem.GetComponent<ParticleSystemRenderer>();
            Undo.RecordObject(renderer, UNDO_LABEL);

            ParticleTextureAnimationSetup.Apply(targetParticleSystem);
            EditorUtility.SetDirty(targetParticleSystem);
            EditorUtility.SetDirty(renderer);
        }
    }
}
