using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class CharacterSelectModelIcon : MonoBehaviour
{
    RawImage rawImage;
    Sprite currentSource;

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

    public void SetPortrait(Sprite portrait)
    {
        EnsureInitialized();

        if (portrait == currentSource) {
            return;
        }

        currentSource = portrait;
        if (portrait == null) {
            rawImage.texture = null;
            rawImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        rawImage.texture = portrait.texture;
        rawImage.uvRect = ResolveUvRect(portrait);
        rawImage.color = Color.white;
    }

    void OnDestroy()
    {
        Teardown();
    }

    void Teardown()
    {
        rawImage.texture = null;
        rawImage.color = new Color(1f, 1f, 1f, 0f);
        currentSource = null;
    }

    static Rect ResolveUvRect(Sprite sprite)
    {
        var texture = sprite.texture;
        var rect = sprite.textureRect;
        return new Rect(
            rect.x / texture.width,
            rect.y / texture.height,
            rect.width / texture.width,
            rect.height / texture.height);
    }
}
