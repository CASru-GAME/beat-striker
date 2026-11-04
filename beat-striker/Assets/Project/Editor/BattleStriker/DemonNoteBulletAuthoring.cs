using System.Collections.Generic;
using System.Linq;
using Core.DarkSatan;
using UnityEditor;
using UnityEngine;

namespace Alice.Editor {
    public static class DemonNoteBulletAuthoring {
        const string Folder = "Assets/Project/Strikers/DarkSatan/Effects/Bullet/";

        [MenuItem("Tools/Strikers/Preview Demon Note Bullets")]
        public static void Preview() {
            var root = PrefabUtility.LoadPrefabContents(Folder + "DemonVolley.prefab");
            var cameraObject = new GameObject("Note preview camera");
            var target = new RenderTexture(1200, 600, 24);
            var texture = new Texture2D(1200, 600, TextureFormat.RGB24, false);
            try {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var serialized = new SerializedObject(root.GetComponent<DemonVolley>());
                var bullets = serialized.FindProperty("bullets");
                var visuals = serialized.FindProperty("bulletVisuals");
                if (bullets.arraySize != 6 || visuals.arraySize != 6) throw new System.InvalidOperationException("Expected six note bullets");
                for (int i = 0; i < 6; i++) {
                    var bullet = (Transform)bullets.GetArrayElementAtIndex(i).objectReferenceValue;
                    bullet.SetPositionAndRotation(new Vector3((i % 3 - 1) * 1.1f, i < 3 ? .45f : -.45f, 0f), Quaternion.LookRotation(i < 3 ? Vector3.right : Vector3.left));
                    foreach (var particles in bullet.GetComponentsInChildren<ParticleSystem>()) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var visual = (DemonNoteBulletVisual)visuals.GetArrayElementAtIndex(i).objectReferenceValue;
                    var symbols = new SerializedObject(visual).FindProperty("symbols");
                    for (int j = 0; j < 3; j++) ((GameObject)symbols.GetArrayElementAtIndex(j).objectReferenceValue).SetActive(j == i % 3);
                }
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = root.scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .02f, .045f);
                camera.orthographic = true;
                camera.orthographicSize = 1.1f;
                camera.transform.position = new Vector3(0f, 0f, -6f);
                camera.targetTexture = target;
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1200, 600), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DemonNoteBullets.png"), texture.EncodeToPNG());
                Debug.Log("Demon note bullet references and both facing directions previewed.");
            } finally {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/Strikers/Generate Demon Note Bullets")]
        public static void Generate() {
            var quarter = new List<Vector2[]>();
            quarter.Add(Ellipse(0f, 0f, .15f, .11f));
            quarter.Add(new[] { new Vector2(.105f, 0f), new Vector2(.155f, 0f), new Vector2(.155f, .66f), new Vector2(.105f, .66f) });
            var eighth = new List<Vector2[]>(quarter);
            eighth.Add(new[] { new Vector2(.13f,.66f), new Vector2(.22f,.59f), new Vector2(.31f,.55f), new Vector2(.35f,.46f), new Vector2(.34f,.36f), new Vector2(.29f,.28f), new Vector2(.30f,.40f), new Vector2(.27f,.46f), new Vector2(.19f,.48f), new Vector2(.13f,.51f) });
            var rest = new List<Vector2[]> {
                new[] { new Vector2(.02f,.67f), new Vector2(.16f,.49f), new Vector2(.04f,.36f), new Vector2(.15f,.22f), new Vector2(.02f,.16f), new Vector2(-.03f,.10f), new Vector2(.02f,.04f), new Vector2(.09f,.03f), new Vector2(.04f,-.025f), new Vector2(-.055f,.015f), new Vector2(-.105f,.10f), new Vector2(-.07f,.20f), new Vector2(.045f,.26f), new Vector2(-.09f,.40f), new Vector2(.045f,.52f), new Vector2(-.04f,.65f) }
            };
            var meshes = new[] { SaveMesh("DemonQuarterNote", quarter), SaveMesh("DemonEighthNote", eighth), SaveMesh("DemonQuarterRest", rest) };
            var names = new[] { "Quarter Note", "Eighth Note", "Quarter Rest" };
            var root = PrefabUtility.LoadPrefabContents(Folder + "DemonVolley.prefab");
            try {
                var volley = root.GetComponent<DemonVolley>();
                var settings = new SerializedObject(volley);
                var bullets = settings.FindProperty("bullets");
                var visuals = settings.FindProperty("bulletVisuals");
                visuals.arraySize = bullets.arraySize;
                for (int i = 0; i < bullets.arraySize; i++) {
                    var bullet = (Transform)bullets.GetArrayElementAtIndex(i).objectReferenceValue;
                    var core = bullet.GetComponentsInChildren<MeshFilter>(true).First().gameObject;
                    var material = core.GetComponent<MeshRenderer>().sharedMaterial;
                    var visual = bullet.GetComponent<DemonNoteBulletVisual>();
                    if (visual == null) visual = bullet.gameObject.AddComponent<DemonNoteBulletVisual>();
                    var visualSettings = new SerializedObject(visual);
                    var symbols = visualSettings.FindProperty("symbols");
                    symbols.arraySize = 3;
                    for (int j = 0; j < 3; j++) {
                        var existing = bullet.Find(names[j]);
                        var symbol = existing != null ? existing.gameObject : j == 0 ? core : new GameObject(names[j]);
                        symbol.name = names[j];
                        symbol.transform.SetParent(bullet, false);
                        symbol.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        symbol.transform.localScale = Vector3.one;
                        var filter = symbol.GetComponent<MeshFilter>();
                        if (filter == null) filter = symbol.AddComponent<MeshFilter>();
                        var renderer = symbol.GetComponent<MeshRenderer>();
                        if (renderer == null) renderer = symbol.AddComponent<MeshRenderer>();
                        filter.sharedMesh = meshes[j];
                        renderer.sharedMaterial = material;
                        symbol.SetActive(j == 0);
                        symbols.GetArrayElementAtIndex(j).objectReferenceValue = symbol;
                    }
                    visualSettings.ApplyModifiedPropertiesWithoutUndo();
                    visuals.GetArrayElementAtIndex(i).objectReferenceValue = visual;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "DemonVolley.prefab");
                Debug.Log("Demon bullets configured: six serialized symbol sets; quarter note, eighth note and quarter rest; noteheads lead along local +Z.");
            } finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        static Vector2[] Ellipse(float x, float y, float width, float height) => Enumerable.Range(0, 32).Select(i => {
            float angle = i * Mathf.PI * 2f / 32f;
            return new Vector2(x + Mathf.Cos(angle) * width, y + Mathf.Sin(angle) * height + Mathf.Cos(angle) * .035f);
        }).ToArray();

        static Mesh SaveMesh(string name, List<Vector2[]> polygons) {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var polygon in polygons) {
                var points = polygon.ToArray();
                float area = 0f;
                for (int i = 0; i < points.Length; i++) area += Cross(points[i], points[(i + 1) % points.Length]);
                if (area < 0f) System.Array.Reverse(points);
                int offset = vertices.Count;
                foreach (float depth in new[] { -.035f, .035f }) {
                    foreach (var p in points) vertices.Add(new Vector3(depth, p.x, -p.y));
                }
                var remaining = Enumerable.Range(0, points.Length).ToList();
                while (remaining.Count > 2) {
                    bool clipped = false;
                    for (int i = 0; i < remaining.Count; i++) {
                        int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                        if (Cross(points[b] - points[a], points[c] - points[b]) <= 0f) continue;
                        if (remaining.Any(p => p != a && p != b && p != c && Inside(points[p], points[a], points[b], points[c]))) continue;
                        triangles.AddRange(new[] { offset+a, offset+c, offset+b, offset+points.Length+a, offset+points.Length+b, offset+points.Length+c });
                        remaining.RemoveAt(i);
                        clipped = true;
                        break;
                    }
                    if (!clipped) throw new System.InvalidOperationException("Cannot triangulate " + name);
                }
                for (int i = 0; i < points.Length; i++) {
                    int a = offset + i, b = offset + (i + 1) % points.Length;
                    triangles.AddRange(new[] { a,b,b+points.Length,a,b+points.Length,a+points.Length });
                }
            }
            var mesh = new Mesh { name = name };
            for (int i = 0; i < triangles.Count; i += 3) {
                int first = triangles[i];
                triangles[i] = triangles[i + 2];
                triangles[i + 2] = first;
            }
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var path = Folder + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) => Cross(b-a,p-a)>=0f && Cross(c-b,p-b)>=0f && Cross(a-c,p-c)>=0f;
    }
}
