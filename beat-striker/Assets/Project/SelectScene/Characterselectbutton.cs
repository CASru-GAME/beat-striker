
using Core;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Botan))]
[RequireComponent(typeof(AudioSource))]
public class Characterselectbutton : MonoBehaviour
{
    Botan botan;
    public RawImage image;
    public AudioClip hoverSound;
    AudioSource audioSource;
    [SerializeField] string strikerId;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        botan = GetComponent<Botan>();
        audioSource = GetComponent<AudioSource>();

        image.color = Color.gray;
        botan.onHover += (e) => {
            image.color = Color.white;
            Debug.Log("hovered");
            if(hoverSound != null && audioSource != null) {
                audioSource.PlayOneShot(hoverSound);
            }
        };
        botan.onClick += (e) => {
            this.GetBus().Publish(new AppMessages.SelectStriker(new PlayerId(e.EventData.pointerId), new StrikerId(strikerId)));
            Debug.Log($"Published SelectStriker for Player {e.EventData.pointerId} and Striker {strikerId}");
            this.GetBus().Publish(new AppMessages.RequireTransition(AppScene.Battle));
        };
        botan.onHoverExit += (e) => {
            image.color = Color.gray;
            Debug.Log("hover exited");
        };
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
