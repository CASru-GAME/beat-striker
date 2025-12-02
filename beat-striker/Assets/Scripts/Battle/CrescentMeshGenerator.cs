using UnityEngine;

namespace Core.Battle {
    // 半月形のメッシュを生成するコンポーネント
    public class CrescentMeshGenerator : MonoBehaviour {
        [SerializeField] private int segments = 32;
        [SerializeField] private float radius = 1f;
        [SerializeField] private float thickness = 0.7f; // 半月の幅（大きいほど太い）
        [SerializeField] private float hueOffset = 0f; // 色相オフセット（0-1）
        
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material rainbowMaterial;
        
        private float pendingHueOffset = -1f; // マテリアル作成前に設定された色相オフセット
        
        void Awake() {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
            
            // 虹色シェーダーのマテリアルを作成
            CreateRainbowMaterial();
            
            GenerateCrescentMesh();
        }
        
        void Start() {
            // マテリアルが作成された後に、保留中の色相オフセットを適用
            if (pendingHueOffset >= 0f) {
                SetHueOffset(pendingHueOffset);
                pendingHueOffset = -1f;
            } else if (hueOffset != 0f) {
                UpdateHueOffset();
            }
        }
        
        void CreateRainbowMaterial() {
            // シェーダーを読み込む
            Shader rainbowShader = Shader.Find("Custom/RainbowGradient");
            if (rainbowShader == null) {
                // シェーダーが見つからない場合はUnlitを使用（フォールバック）
                rainbowShader = Shader.Find("Unlit/Color");
                Debug.LogWarning("RainbowGradient shader not found, using Unlit/Color as fallback");
            }
            
            rainbowMaterial = new Material(rainbowShader);
            if (rainbowShader.name == "Custom/RainbowGradient") {
                rainbowMaterial.SetFloat("_Intensity", 1.0f);
                rainbowMaterial.SetFloat("_Speed", 1.0f);
                rainbowMaterial.SetFloat("_Saturation", 1.0f);
                rainbowMaterial.SetFloat("_HueOffset", hueOffset);
            } else {
                rainbowMaterial.color = Color.white;
            }
            
            meshRenderer.material = rainbowMaterial;
        }
        
        void GenerateCrescentMesh() {
            Mesh mesh = new Mesh();
            mesh.name = "CrescentMesh";
            
            // 半月形を生成（縦方向、Y-Z平面、前方に向かって垂直、端を尖らせる）
            int vertexCount = (segments + 1) * 2; // 外側と内側の頂点
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[segments * 6];
            
            // 端を尖らせるためのパラメータ
            float taperRange = 0.15f; // 端の何割を尖らせるか（0.15 = 15%）
            
            for (int i = 0; i <= segments; i++) {
                float t = (float)i / segments; // 0 to 1
                float angle = Mathf.PI * t; // 0 to π (180度)
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                
                // 端の部分でthicknessを小さくする（尖らせる）
                float currentThickness = thickness;
                if (t < taperRange) {
                    // 左端：0からtaperRangeまで
                    float taperT = t / taperRange;
                    currentThickness = Mathf.Lerp(0f, thickness, taperT * taperT); // 2乗で滑らかに
                } else if (t > 1f - taperRange) {
                    // 右端：1-taperRangeから1まで
                    float taperT = (1f - t) / taperRange;
                    currentThickness = Mathf.Lerp(0f, thickness, taperT * taperT); // 2乗で滑らかに
                }
                
                // Y-Z平面で生成（X軸は0、縦方向に配置）
                // 外側の頂点
                vertices[i] = new Vector3(0, cos * radius, sin * radius);
                uv[i] = new Vector2(t, 0);
                
                // 内側の頂点（端で尖らせる）
                float innerRadius = radius - currentThickness;
                vertices[i + segments + 1] = new Vector3(0, cos * innerRadius, sin * innerRadius);
                uv[i + segments + 1] = new Vector2(t, 1);
            }
            
            // 三角形を生成
            int triIndex = 0;
            for (int i = 0; i < segments; i++) {
                int outer1 = i;
                int outer2 = i + 1;
                int inner1 = i + segments + 1;
                int inner2 = i + segments + 2;
                
                // 最初の三角形（時計回り）
                triangles[triIndex++] = outer1;
                triangles[triIndex++] = outer2;
                triangles[triIndex++] = inner1;
                
                // 2番目の三角形（時計回り）
                triangles[triIndex++] = outer2;
                triangles[triIndex++] = inner2;
                triangles[triIndex++] = inner1;
            }
            
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            meshFilter.mesh = mesh;
        }
        
        void OnValidate() {
            if (Application.isPlaying && meshFilter != null) {
                GenerateCrescentMesh();
                UpdateHueOffset();
            }
        }
        
        // 色相オフセットを設定するパブリックメソッド
        public void SetHueOffset(float offset) {
            hueOffset = Mathf.Clamp01(offset);
            if (rainbowMaterial != null) {
                UpdateHueOffset();
            } else {
                // マテリアルがまだ作成されていない場合は保留
                pendingHueOffset = offset;
            }
        }
        
        void UpdateHueOffset() {
            if (rainbowMaterial != null && meshRenderer != null && meshRenderer.material != null) {
                rainbowMaterial.SetFloat("_HueOffset", hueOffset);
            }
        }
    }
}

