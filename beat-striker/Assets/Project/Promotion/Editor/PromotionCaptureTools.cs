using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alice.Editor {
    public static class PromotionCaptureTools {
        const string Output = "Assets/Project/Promotion";
        const string HeroPath = "Assets/Project/Strikers/LargeHero/Hero.prefab";
        const string WizardPath = "Assets/Project/Strikers/LargeWizard/Wizard1.prefab";

        sealed class Resources {
            readonly Mesh container;
            readonly Dictionary<string, UnityEngine.Object> entries;
            public Resources() {
                Directory.CreateDirectory(Output);
                const string path = Output + "/PromotionResources.asset";
                container = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!container) {
                    container = new Mesh { name = "PromotionResources" };
                    AssetDatabase.CreateAsset(container, path);
                }
                entries = AssetDatabase.LoadAllAssetsAtPath(path).ToDictionary(a => a.name);
            }
            public T Keep<T>(T obj, string name) where T : UnityEngine.Object {
                obj.name = name;
                if (entries.TryGetValue(name, out var old)) {
                    EditorUtility.CopySerialized(obj, old);
                    old.hideFlags = HideFlags.None;
                    EditorUtility.SetDirty(old);
                    UnityEngine.Object.DestroyImmediate(obj);
                    return (T)old;
                }
                obj.name = name;
                obj.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(obj, container);
                entries.Add(name, obj);
                return obj;
            }
            public Material Lit(string name, Color color, float metal = 0, float smoothness = .5f) {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Metallic", metal);
                m.SetFloat("_Smoothness", smoothness);
                return Keep(m, name);
            }
            public Material Glow(string name, Color color, float intensity = 3) {
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.SetColor("_BaseColor", new Color(color.r * intensity, color.g * intensity, color.b * intensity, 1));
                m.SetFloat("_Cull", 0);
                return Keep(m, name);
            }
            public void PersistMaterials(GameObject root, string prefix) {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
                    r.sharedMaterials = r.sharedMaterials.Select(m => {
                        if (!m) return m;
                        var copy = new Material(m);
                        if (copy.shader.name.Contains("MToon") && copy.HasProperty("_EmissionColor")) copy.SetColor("_EmissionColor", Color.black);
                        return Keep(copy, prefix + "_" + m.name.Replace(" (Instance)", ""));
                    }).ToArray();
                }
            }
        }

        sealed class Actor {
            public GameObject Root;
            public Transform Model;
            public Transform Weapon;
            public Transform Head;
        }

        static Actor Character(Resources res, bool hero, string state, float time, Vector3 position, float yaw) {
            var path = hero ? HeroPath : WizardPath;
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            root.name = hero ? "Hero — posed game model" : "Wizard — posed game model";
            var model = root.transform.Find(hero ? "Hero" : "Magic.bone");
            var gear = root.transform.Find(hero ? "Gear/sword" : "Gear/tue");
            var source = root.GetComponentsInChildren<MonoBehaviour>(true).Single(b => b && b.gameObject.name == state && new SerializedObject(b).FindProperty("animationClip.clip") != null);
            var clip = (AnimationClip)new SerializedObject(source).FindProperty("animationClip.clip").objectReferenceValue;
            var tracker = gear.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "Tracker");
            var target = (Transform)new SerializedObject(tracker).FindProperty("target").objectReferenceValue;
            gear.SetParent(target, true);
            res.PersistMaterials(root, hero ? "Hero" : "Wizard");
            foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true)) if (b) b.enabled = false;
            foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) if (b) UnityEngine.Object.DestroyImmediate(b);
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var t in root.transform.Cast<Transform>().ToArray()) if (t != model) UnityEngine.Object.DestroyImmediate(t.gameObject);
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true)) UnityEngine.Object.DestroyImmediate(trail.gameObject);
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var graph = PlayableGraph.Create("Promotion pose sampling");
            try {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                output.SetSourcePlayable(playable);
                playable.SetTime(time);
                graph.Play();
                graph.Evaluate(0);
                if (animator.avatar && animator.avatar.isHuman) {
                    using (var handler = new HumanPoseHandler(animator.avatar, model)) {
                        var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                        var side = target.name.Contains("_R_") ? "Right" : "Left";
                        foreach (var finger in new[] { "Index", "Middle", "Ring", "Little" }) {
                            for (var segment = 1; segment <= 3; segment++) {
                                var index = Array.FindIndex(HumanTrait.MuscleName, n => n.StartsWith(side, StringComparison.Ordinal) && n.Contains(finger) && n.Contains(segment.ToString()) && n.EndsWith("Stretched", StringComparison.Ordinal));
                                if (index >= 0) pose.muscles[index] = -.75f;
                            }
                        }
                        handler.SetHumanPose(ref pose);
                    }
                }
                animator.enabled = false;
            } finally { graph.Destroy(); }
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            foreach (var a in root.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                skin.updateWhenOffscreen = true;
                skin.localBounds = new Bounds(Vector3.up, Vector3.one * 8);
            }
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var head = model.GetComponentsInChildren<Transform>().Single(t => t.name == "J_Bip_C_Head");
            root.name += " [" + clip.name + " @ " + time.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "s]";
            return new Actor { Root = root, Model = model, Weapon = gear, Head = head };
        }

        static void AimSword(Actor actor, Vector3 direction) {
            var bounds = actor.Weapon.GetComponent<MeshFilter>().sharedMesh.bounds;
            // The game's sword mesh is centred on its full length; its blade points along local +Z.
            var grip = bounds.center - Vector3.forward * bounds.size.z * .32f;
            actor.Weapon.rotation = Quaternion.LookRotation(direction.normalized, Vector3.back);
            actor.Weapon.position += actor.Weapon.parent.position - actor.Weapon.TransformPoint(grip);
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat) {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = name; o.transform.SetParent(parent, false);
            o.transform.localPosition = pos; o.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
            o.GetComponent<Renderer>().sharedMaterial = mat;
            return o;
        }

        static Light Light(string name, Transform parent, Vector3 position, Color color, float intensity, float range = 15, LightType type = LightType.Point) {
            var o = new GameObject(name); o.transform.SetParent(parent, false); o.transform.localPosition = position;
            var light = o.AddComponent<Light>(); light.type = type; light.color = color; light.intensity = intensity;
            light.range = range; light.shadows = LightShadows.Soft;
            return light;
        }

        static void Line(string name, Transform parent, Vector3[] pts, float width, Material mat, bool loop = false) {
            var o = new GameObject(name); o.transform.SetParent(parent, false);
            var l = o.AddComponent<LineRenderer>(); l.useWorldSpace = false; l.sharedMaterial = mat;
            l.loop = loop; l.positionCount = pts.Length; l.SetPositions(pts);
            l.widthMultiplier = width; l.numCornerVertices = 4; l.numCapVertices = 4;
            l.shadowCastingMode = ShadowCastingMode.Off; l.receiveShadows = false;
        }

        static void Ring(string name, Transform parent, Vector3 center, Vector3 normal, float radius, float width, Material mat, float start = 0, float extent = 360) {
            var rotation = Quaternion.FromToRotation(Vector3.forward, normal.normalized);
            var count = Mathf.CeilToInt(extent / 3);
            var pts = Enumerable.Range(0, count + 1).Select(i => {
                var a = (start + extent * i / count) * Mathf.Deg2Rad;
                return center + rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
            }).ToArray();
            Line(name, parent, pts, width, mat, extent >= 360);
        }

        static void ArcRibbon(Resources res, string name, Transform parent, Vector3 center, float radius, float width, float start, float extent, Material mat, Quaternion rotation) {
            const int segments = 100;
            var vertices = new Vector3[(segments + 1) * 2];
            var tris = new int[segments * 6];
            for (var i = 0; i <= segments; i++) {
                var t = (float)i / segments;
                var a = (start + extent * t) * Mathf.Deg2Rad;
                var taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .7f);
                var v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                vertices[i * 2] = center + rotation * (v * (radius - width * taper));
                vertices[i * 2 + 1] = center + rotation * (v * radius);
                if (i == segments) continue;
                var k = i * 6; var j = i * 2;
                tris[k] = j; tris[k + 1] = j + 1; tris[k + 2] = j + 2;
                tris[k + 3] = j + 1; tris[k + 4] = j + 3; tris[k + 5] = j + 2;
            }
            var mesh = new Mesh { vertices = vertices, triangles = tris };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var o = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(parent, false);
            o.GetComponent<MeshFilter>().sharedMesh = res.Keep(mesh, name);
            o.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void Sigil(Resources res, Transform parent, Vector3 center, Vector3 normal, float radius, Material bright, Material dim, string prefix) {
            var rotation = Quaternion.FromToRotation(Vector3.forward, normal);
            Ring(prefix + " outer", parent, center, normal, radius, .012f, bright);
            Ring(prefix + " inner", parent, center, normal, radius * .89f, .008f, dim);
            Ring(prefix + " core", parent, center, normal, radius * .57f, .008f, dim);
            for (var i = 0; i < 12; i++) {
                var a = i * Mathf.PI / 6;
                var v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                Line(prefix + " radial mark " + i, parent, new[] { center + rotation * v * radius * .92f, center + rotation * v * radius * .97f }, .016f, bright);
            }
            for (var i = 0; i < 2; i++) {
                var pts = Enumerable.Range(0, 4).Select(j => {
                    var a = (j * 120 + i * 180 + 30) * Mathf.Deg2Rad;
                    return center + rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius * .77f;
                }).ToArray();
                Line(prefix + " triangle " + i, parent, pts, .009f, dim);
            }
        }

        static void Sparks(Transform parent, Vector3 center, Material warm, Material cool, int seed, int count, float spread = 1) {
            var rand = new System.Random(seed);
            for (var i = 0; i < count; i++) {
                var angle = (float)rand.NextDouble() * Mathf.PI * 2;
                var dist = Mathf.Pow((float)rand.NextDouble(), .6f) * spread;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), ((float)rand.NextDouble() - .5f) * .5f);
                var p = center + dir * dist;
                var len = (.015f + (float)rand.NextDouble() * .12f) * spread;
                Line("Suspended impact spark " + i, parent, new[] { p, p + dir * len }, .003f + (float)rand.NextDouble() * .009f, i % 5 == 0 ? cool : warm);
            }
        }

        static void Environment(Resources res, Transform parent, int shot) {
            var stone = res.Lit("Obsidian arena", new Color(.055f, .07f, .11f), .35f, .44f);
            var edge = res.Lit("Basalt edges", new Color(.043f, .06f, .095f), .3f, .5f);
            var cyan = res.Glow("Arena cyan", new Color(.015f, .34f, .7f), 1.7f);
            var amber = res.Glow("Arena amber", new Color(1, .29f, .025f), 2);
            Box("Reflective dark arena", parent, new Vector3(0, -.15f, 0), new Vector3(40, .25f, 40), stone);
            for (var i = -10; i < 11; i++) {
                Box("Floor seam X " + i, parent, new Vector3(i * 1.2f, -.018f, 0), new Vector3(.012f, .007f, 32), edge);
                Box("Floor seam Z " + i, parent, new Vector3(0, -.017f, i * 1.2f), new Vector3(32, .007f, .012f), edge);
            }
            for (var i = 0; i < 7; i++) {
                var x = (i - 3) * 2.2f;
                var h = 2.6f + Mathf.Sin(i * 2.7f) * .6f;
                Box("Distant monolith " + i, parent, new Vector3(x, h / 2, 5.2f + Mathf.Cos(i) * .5f), new Vector3(.6f, h, .7f), edge);
                Box("Monolith foot " + i, parent, new Vector3(x, .13f, 5.2f + Mathf.Cos(i) * .5f), new Vector3(.85f, .26f, .9f), edge);
                Box("Monolith crown " + i, parent, new Vector3(x, h, 5.2f + Mathf.Cos(i) * .5f), new Vector3(.8f, .15f, .8f), edge);
                Box("Monolith light " + i, parent, new Vector3(x - .24f, h / 2, 4.8f + Mathf.Cos(i) * .5f), new Vector3(.018f, h * .82f, .025f), i % 3 == 0 ? amber : cyan);
            }
            for (var i = 0; i < 3; i++) Ring("Arena luminous circuit " + i, parent, new Vector3(0, -.008f + i * .002f, 0), Vector3.up, 3.8f + i * .16f, .008f, cyan);
            var rand = new System.Random(62 + shot);
            for (var i = 0; i < 20; i++) {
                var x = ((float)rand.NextDouble() - .5f) * 10;
                var z = (float)rand.NextDouble() * 4 + 1.7f;
                var scale = .05f + (float)rand.NextDouble() * .17f;
                var o = Box("Suspended arena shard " + i, parent, new Vector3(x, .12f + (float)rand.NextDouble() * 2, z), Vector3.one * scale, edge);
                o.transform.localEulerAngles = new Vector3(i * 37, i * 21, i * 47);
            }
        }

        static Camera Camera(Transform parent, Vector3 position, Vector3 target, float lens, float roll) {
            var o = new GameObject("Advertising camera"); o.transform.SetParent(parent, false); o.transform.position = position;
            o.transform.LookAt(target); o.transform.Rotate(0, 0, roll, Space.Self);
            var c = o.AddComponent<Camera>(); c.fieldOfView = lens; c.nearClipPlane = .05f; c.farClipPlane = 100;
            c.clearFlags = CameraClearFlags.Skybox; c.backgroundColor = new Color(.025f, .055f, .12f);
            c.allowHDR = true; c.allowMSAA = true;
            var data = c.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            return c;
        }

        static void Post(Resources res, Transform parent, int shot) {
            var go = new GameObject("Cinematic grade"); go.transform.SetParent(parent, false);
            var v = go.AddComponent<Volume>(); v.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(); bloom.intensity.Override(.24f); bloom.threshold.Override(1.3f); bloom.scatter.Override(.55f);
            var grade = profile.Add<ColorAdjustments>(); grade.postExposure.Override(-.2f); grade.contrast.Override(9); grade.saturation.Override(6);
            var tone = profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var vignette = profile.Add<Vignette>(); vignette.intensity.Override(.22f); vignette.smoothness.Override(.6f);
            var dof = profile.Add<DepthOfField>(); dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(shot == 0 ? 2.8f : shot == 1 ? 4 : 4.2f);
            dof.aperture.Override(5.6f); dof.focalLength.Override(45);
            // Profiles and their components are persistent sub-assets, so reopening the scene retains the grade.
            for (var i = 0; i < profile.components.Count; i++) profile.components[i] = res.Keep(profile.components[i], "Shot " + shot + " grade " + profile.components[i].GetType().Name);
            v.sharedProfile = res.Keep(profile, "Shot " + shot + " cinematic grade");
        }

        static Camera Compose(Resources res, int shot) {
            var set = new GameObject("Advertising set " + (shot + 1));
            var p = set.transform;
            Environment(res, p, shot);
            Post(res, p, shot);
            var key = Light("Cool moon key", p, new Vector3(-2, 5, -4), new Color(.95f, .92f, 1), .8f, 20, LightType.Directional);
            key.transform.rotation = Quaternion.Euler(25, -20, 0);
            Light("Warm face fill", p, new Vector3(0, 2.5f, -3), new Color(1, .85f, .69f), 1.2f, 10);
            Light("Blue edge light", p, new Vector3(1, 3, 3), new Color(.15f, .48f, 1), 1.8f, 12);
            var gold = res.Glow("Sword warm core", new Color(1, .52f, .06f), 2.7f);
            var white = res.Glow("Impact white core", new Color(1, .88f, .59f), 9);
            var blue = res.Glow("Spell azure core", new Color(.035f, .47f, 1), 6);
            var dim = res.Glow("Spell azure filaments", new Color(.025f, .17f, .5f), 3);
            Actor hero, wizard;
            Camera camera;
            if (shot == 0) {
                hero = Character(res, true, "Slash1State", .19f, new Vector3(-.85f, .02f, -.15f), 130);
                wizard = Character(res, false, "Attack1State", .22f, new Vector3(.9f, .02f, .15f), 244);
                AimSword(hero, new Vector3(1, .035f, .08f));
                camera = Camera(p, new Vector3(0, 1.5f, -2.55f), new Vector3(0, 1.3f, 0), 43, -5);
                var impact = new Vector3(.05f, 1.15f, -.22f);
                Sigil(res, p, impact + new Vector3(.14f, 0, .06f), new Vector3(-.65f, 0, -1), .68f, blue, dim, "Clash shield");
                ArcRibbon(res, "Clash sword crescent", p, new Vector3(-.55f, 1.15f, -.1f), .78f, .045f, -35, 175, gold, Quaternion.Euler(15, -10, -18));
                Ring("Clash shockwave", p, impact, Vector3.forward, .12f, .009f, white);
                Sparks(p, impact, gold, blue, 34, 85, 1.25f);
                Light("Clash warm bounce", p, impact + Vector3.back * .4f, new Color(1, .36f, .025f), .45f, 5);
                Light("Clash azure bounce", p, impact + Vector3.right * .3f, new Color(.03f, .29f, 1), .5f, 5);
            } else if (shot == 1) {
                hero = Character(res, true, "Slash2State", .18f, new Vector3(-.7f, .02f, -.65f), 145);
                wizard = Character(res, false, "Attack2State", .6f, new Vector3(1.4f, .02f, 1), 235);
                AimSword(hero, new Vector3(1, .12f, .25f));
                camera = Camera(p, new Vector3(-.4f, 1.15f, -3.6f), new Vector3(.2f, .95f, .2f), 44, 7);
                ArcRibbon(res, "Rush sweeping sword trail", p, new Vector3(-.25f, .8f, -.4f), 1.35f, .065f, -60, 210, gold, Quaternion.Euler(68, 10, -25));
                Sigil(res, p, new Vector3(.75f, 1.05f, .3f), new Vector3(-.3f, 0, -1), .58f, blue, dim, "Rush counterspell");
                Sparks(p, new Vector3(.1f, .9f, -.3f), gold, blue, 54, 66, 1.6f);
                Light("Rush sword bounce", p, new Vector3(-.8f, .65f, -1.3f), new Color(1, .3f, .02f), .5f, 5);
            } else {
                hero = Character(res, true, "AirAttackState", .2f, new Vector3(-.85f, .6f, .2f), 125);
                wizard = Character(res, false, "Attack1State", .28f, new Vector3(1.05f, .06f, -.45f), 235);
                AimSword(hero, new Vector3(1, -.4f, -.1f));
                camera = Camera(p, new Vector3(1.1f, 1.5f, -3.9f), new Vector3(.1f, 1.2f, .1f), 44, -6);
                blue = res.Glow("Special violet core", new Color(.46f, .12f, 1), 5);
                dim = res.Glow("Special violet filaments", new Color(.15f, .035f, .42f), 3);
                Sigil(res, p, new Vector3(1.1f, 1.4f, .8f), new Vector3(0, 0, -1), 1.2f, blue, dim, "Special floating geometry");
                Ring("Special ground pulse", p, new Vector3(.75f, .016f, -.1f), Vector3.up, 1.4f, .023f, blue);
                Ring("Special ground pulse outer", p, new Vector3(.75f, .018f, -.1f), Vector3.up, 1.55f, .009f, dim);
                ArcRibbon(res, "Aerial sword crescent", p, new Vector3(-.8f, 1.4f, .2f), .9f, .12f, 20, 200, gold, Quaternion.Euler(10, -20, 35));
                Sparks(p, new Vector3(.1f, 1.4f, 0), gold, blue, 78, 90, 1.7f);
                Light("Special violet bounce", p, new Vector3(1, 1.5f, -.2f), new Color(.4f, .08f, 1), .6f, 6);
            }
            hero.Root.transform.SetParent(p, true); wizard.Root.transform.SetParent(p, true);
            hero.Head.rotation = Quaternion.Euler(0, shot == 2 ? 140 : 130, 0);
            wizard.Head.rotation = Quaternion.Euler(0, shot == 2 ? 205 : 220, 0);
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunDisk", 0); sky.SetFloat("_Exposure", .5f); sky.SetFloat("_AtmosphereThickness", .65f);
            sky.SetColor("_SkyTint", new Color(.025f, .07f, .2f)); sky.SetColor("_GroundColor", new Color(.008f, .018f, .045f));
            RenderSettings.skybox = res.Keep(sky, "Arena midnight sky");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.32f, .38f, .5f);
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.32f, .38f, .5f));
            RenderSettings.ambientProbe = ambient;
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.025f, .055f, .12f);
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .045f;
            return camera;
        }

        static void Capture(Camera camera, string path, int width) {
            var height = width * 9 / 16;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 4 };
            var encoded = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var old = RenderTexture.active;
            var oldSrgb = GL.sRGBWrite;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try {
                camera.targetTexture = rt;
                camera.aspect = 16f / 9;
                camera.Render();
                GL.sRGBWrite = true;
                Graphics.Blit(rt, encoded);
                RenderTexture.active = encoded;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            } finally {
                camera.targetTexture = null; RenderTexture.active = old;
                GL.sRGBWrite = oldSrgb;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
                encoded.Release(); UnityEngine.Object.DestroyImmediate(encoded);
            }
        }

        [MenuItem("Beat Striker/Promotion/Create three advertising scenes")]
        public static void CreateScenes() {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring stills.");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes before authoring stills.");
            for (var i = 1; i <= 3; i++) {
                var path = Output + "/Duel" + i + ".unity";
                if (File.Exists(path)) throw new IOException("A shot already exists: " + path);
            }
            var previous = SceneManager.GetActiveScene().path;
            try {
                for (var i = 0; i < 3; i++) {
                    var scenePath = Output + "/Duel" + (i + 1) + ".unity";
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var res = new Resources();
                    var camera = Compose(res, i);
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.SaveScene(scene, scenePath);
                    Capture(camera, "Temp/duel-preview-" + (i + 1) + ".png", 1280);
                }
                Debug.Log("Three editable advertising scenes and 16:9 previews created.");
            } finally { if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous); }
        }

        [MenuItem("Beat Striker/Promotion/Render three final images")]
        public static void RenderFinalImages() {
            RenderSavedShots(2560, Output + "/Duel");
        }

        [MenuItem("Beat Striker/Promotion/Render three previews")]
        public static void RenderPreviews() {
            RenderSavedShots(1280, "Temp/duel-preview-");
        }

        static void RenderSavedShots(int width, string prefix) {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before rendering stills.");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes before rendering stills.");
            var previous = SceneManager.GetActiveScene().path;
            try {
                for (var i = 0; i < 3; i++) {
                    var scene = EditorSceneManager.OpenScene(Output + "/Duel" + (i + 1) + ".unity");
                    VerifyShot(scene);
                    var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).Single();
                    Capture(camera, prefix + (i + 1) + ".png", width);
                }
            } finally { if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous); }
            AssetDatabase.Refresh();
        }

        static void VerifyShot(Scene scene) {
            var roots = scene.GetRootGameObjects();
            var skins = roots.SelectMany(r => r.GetComponentsInChildren<SkinnedMeshRenderer>()).ToArray();
            if (skins.Length != 8) throw new InvalidOperationException("Expected the three hero and five wizard skinned meshes in " + scene.path);
            foreach (var skin in skins) {
                if (!skin.sharedMesh || skin.bones.Any(b => !b)) throw new InvalidOperationException("A model mesh or bone reference is missing: " + skin.name + " in " + scene.path);
            }
            foreach (var renderer in roots.SelectMany(r => r.GetComponentsInChildren<Renderer>())) {
                if (renderer.sharedMaterials.Any(m => !m || !m.shader)) throw new InvalidOperationException("A material reference is missing in " + scene.path);
            }
            if (roots.SelectMany(r => r.GetComponentsInChildren<Animator>()).Any(a => a.enabled)) throw new InvalidOperationException("The sampled poses must remain frozen in " + scene.path);
            var volume = roots.SelectMany(r => r.GetComponentsInChildren<Volume>()).Single();
            if (!volume.sharedProfile || volume.sharedProfile.components.Count != 5 || volume.sharedProfile.components.Any(c => !c)) throw new InvalidOperationException("A cinematic grade component is missing in " + scene.path);
            Debug.Log("Verified reopened promotional scene: " + scene.path + " — 8 skinned meshes, bone/material references and cinematic grade retained.");
        }

    }
}
