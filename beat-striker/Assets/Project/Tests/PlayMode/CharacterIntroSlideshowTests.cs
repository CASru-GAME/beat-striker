using System.Collections;
using System.Reflection;
using Alice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CharacterIntroSlideshowTests {
    [UnityTest]
    public IEnumerator StartAndDuplicateInputsDoNotSkipSlides() {
        var owner = new GameObject("Slideshow test");
        var texture = new Texture2D(1, 1);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        try {
            var slideshow = owner.AddComponent<CharacterIntroSlideshow>();
            var overlay = new GameObject("Overlay", typeof(RectTransform));
            overlay.transform.SetParent(owner.transform);
            var slide = new GameObject("Slide", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            slide.transform.SetParent(overlay.transform);
            foreach (var item in new[] { ("overlayRoot", (object)overlay), ("slideRectTransform", (object)slide.GetComponent<RectTransform>()), ("slideImage", (object)slide.GetComponent<UnityEngine.UI.Image>()) })
                typeof(CharacterIntroSlideshow).GetField(item.Item1, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slideshow, item.Item2);
            typeof(CharacterIntroSlideshow).GetField("wizardSlides", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(slideshow, new[] { sprite, sprite });

            var playback = slideshow.PlayAsync(Striker.Wizard);
            slideshow.Advance(); // The input that started playback.
            Assert.That(playback.IsCompleted, Is.False);

            yield return null;
            slideshow.Advance(); // The first physical press can arrive through two input paths.
            slideshow.Advance();
            Assert.That(playback.IsCompleted, Is.False);

            yield return null;
            slideshow.Advance();
            Assert.That(playback.IsCompleted, Is.True);
            Assert.That(playback.Result, Is.True);
        }
        finally {
            Object.Destroy(owner);
            Object.Destroy(sprite);
            Object.Destroy(texture);
        }
    }
}
