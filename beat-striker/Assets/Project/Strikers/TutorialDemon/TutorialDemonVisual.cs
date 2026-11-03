using UnityEngine;

namespace Alice {
    // Temporary visuals for the tutorial opponent. The underlying fighter keeps its
    // existing collision, states, effects and animations until a final model is made.
    public sealed class TutorialDemonVisual : MonoBehaviour {
        Material bodyMaterial;
        Material hornMaterial;
        Material eyeMaterial;
        Transform figure;

        void Awake() {
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                renderer.enabled = false;
            }
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true)) {
                renderer.enabled = false;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            bodyMaterial = NewMaterial(shader, new Color(0.19f, 0.09f, 0.24f));
            hornMaterial = NewMaterial(shader, new Color(0.52f, 0.25f, 0.58f));
            eyeMaterial = NewMaterial(shader, new Color(1f, 0.19f, 0.24f));
            figure = new GameObject("Mock Demon").transform;
            figure.SetParent(transform, false);

            Part("Head", PrimitiveType.Sphere, new Vector3(0, 1.48f, 0), new Vector3(.48f, .46f, .38f), Vector3.zero, bodyMaterial);
            Part("Body", PrimitiveType.Capsule, new Vector3(0, .94f, 0), new Vector3(.18f, .32f, .16f), Vector3.zero, bodyMaterial);
            Part("Left arm", PrimitiveType.Capsule, new Vector3(-.27f, 1.04f, 0), new Vector3(.10f, .28f, .10f), new Vector3(0, 0, -53), bodyMaterial);
            Part("Right arm", PrimitiveType.Capsule, new Vector3(.27f, 1.04f, 0), new Vector3(.10f, .28f, .10f), new Vector3(0, 0, 53), bodyMaterial);
            Part("Left leg", PrimitiveType.Capsule, new Vector3(-.14f, .39f, 0), new Vector3(.11f, .31f, .11f), new Vector3(0, 0, -15), bodyMaterial);
            Part("Right leg", PrimitiveType.Capsule, new Vector3(.14f, .39f, 0), new Vector3(.11f, .31f, .11f), new Vector3(0, 0, 15), bodyMaterial);
            Part("Left horn", PrimitiveType.Capsule, new Vector3(-.20f, 1.74f, 0), new Vector3(.10f, .20f, .10f), new Vector3(0, 0, 30), hornMaterial);
            Part("Right horn", PrimitiveType.Capsule, new Vector3(.20f, 1.74f, 0), new Vector3(.10f, .20f, .10f), new Vector3(0, 0, -30), hornMaterial);
            // Eyes on both sides keep the simple face visible when the fighter turns.
            foreach (var z in new[] { -.185f, .185f }) {
                Part("Left eye", PrimitiveType.Sphere, new Vector3(-.105f, 1.50f, z), new Vector3(.13f, .045f, .035f), new Vector3(0, 0, -12), eyeMaterial);
                Part("Right eye", PrimitiveType.Sphere, new Vector3(.105f, 1.50f, z), new Vector3(.13f, .045f, .035f), new Vector3(0, 0, 12), eyeMaterial);
            }
        }

        static Material NewMaterial(Shader shader, Color color) {
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        void Part(string partName, PrimitiveType primitive, Vector3 position, Vector3 scale, Vector3 rotation, Material material) {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.transform.SetParent(figure, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localEulerAngles = rotation;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
        }

        void Update() {
            if (figure != null) figure.localPosition = new Vector3(0, Mathf.Sin(Time.time * 3f) * .025f, 0);
        }

        void OnDestroy() {
            Destroy(bodyMaterial);
            Destroy(hornMaterial);
            Destroy(eyeMaterial);
        }
    }
}
