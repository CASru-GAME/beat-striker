using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class CharacterSelectModelIcon : MonoBehaviour
{
    const int PREVIEW_LAYER = 30;
    const float SPACING = 4f;
    const float BASE_Y = -1000f;
    const int RT_SIZE = 128;

    static int counter;

    RenderTexture rt;
    Camera cam;
    Light previewLight;
    RawImage rawImage;
    GameObject model;
    Vector3 rotCenter;
    float rotSpeed = 50f;
    GameObject currentSource;
    [SerializeField] bool enableModelPreview = true;

    void Awake()
    {
        EnsureInitialized();
    }

    void EnsureInitialized()
    {
        if (rawImage != null) {
            return;
        }

        rawImage = GetComponent<RawImage>();
        rawImage.color = new Color(1f, 1f, 1f, 0f);
    }

    public void SetModel(GameObject modelPrefab, float viewAngle = 35f)
    {
        EnsureInitialized();

        if (!enableModelPreview) {
            currentSource = modelPrefab;
            Teardown();
            rawImage.texture = null;
            rawImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        if (modelPrefab == currentSource) {
            return;
        }

        currentSource = modelPrefab;
        Teardown();

        if (modelPrefab == null) {
            rawImage.texture = null;
            rawImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        var idx = counter++;
        var pos = new Vector3(idx * SPACING, BASE_Y, 0f);

        rt = new RenderTexture(RT_SIZE, RT_SIZE, 16, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 2;
        rt.Create();
        rawImage.texture = rt;
        rawImage.color = Color.white;

        model = Instantiate(modelPrefab);
        ForceActivateRecursive(model);
        model.transform.position = pos;
        SetLayerRecursive(model, PREVIEW_LAYER);

        var colliders = model.GetComponentsInChildren<Collider>(true);
        for (var i = 0; i < colliders.Length; i++) {
            colliders[i].enabled = false;
        }

        var rigidbodies = model.GetComponentsInChildren<Rigidbody>(true);
        for (var i = 0; i < rigidbodies.Length; i++) {
            rigidbodies[i].detectCollisions = false;
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].linearVelocity = Vector3.zero;
            rigidbodies[i].angularVelocity = Vector3.zero;
        }

        // 描画関連まで止めないよう、MonoBehaviourは一律停止しない。
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++) {
            renderers[i].enabled = true;
        }

        var bounds = GetBounds(model);
        rotCenter = bounds.center;
        var size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.01f);

        var camGo = new GameObject($"_CharacterPreviewCam_{idx}");
        var distance = size * 2.5f;
        var angleRad = viewAngle * Mathf.Deg2Rad;
        var offset = new Vector3(0f, distance * Mathf.Sin(angleRad), -distance * Mathf.Cos(angleRad));
        camGo.transform.position = rotCenter + offset;
        camGo.transform.LookAt(rotCenter);

        cam = camGo.AddComponent<Camera>();
        cam.targetTexture = rt;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << PREVIEW_LAYER;
        cam.fieldOfView = 25f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = size * 20f;
        cam.allowHDR = false;
        cam.depth = -100f;
        cam.enabled = true;

        var lightGo = new GameObject($"_CharacterPreviewLight_{idx}");
        lightGo.transform.position = camGo.transform.position + new Vector3(-0.3f, 0.5f, -0.2f) * size;
        lightGo.transform.LookAt(rotCenter);
        previewLight = lightGo.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.1f;
        previewLight.cullingMask = 1 << PREVIEW_LAYER;

        // メインカメラのマスクは変更しない。
    }

    void Update()
    {
        if (model == null) {
            return;
        }

        model.transform.RotateAround(rotCenter, Vector3.up, rotSpeed * Time.deltaTime);
    }

    void OnDestroy()
    {
        Teardown();
    }

    void Teardown()
    {
        if (model != null) {
            Destroy(model);
            model = null;
        }

        if (cam != null) {
            Destroy(cam.gameObject);
            cam = null;
        }

        if (previewLight != null) {
            Destroy(previewLight.gameObject);
            previewLight = null;
        }

        if (rt != null) {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    static void ForceActivateRecursive(GameObject go)
    {
        go.SetActive(true);
        foreach (Transform child in go.transform) {
            ForceActivateRecursive(child.gameObject);
        }
    }

    static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) {
            return new Bounds(go.transform.position, Vector3.one * 0.1f);
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }
}
