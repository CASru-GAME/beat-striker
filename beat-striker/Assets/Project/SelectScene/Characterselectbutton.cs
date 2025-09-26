
using UnityEngine;
using UnityEngine.UI;

public class Characterselectbutton : MonoBehaviour
{
    Botan botan;
    public RawImage image;
    public AudioClip hoverSound;
    AudioSource audioSource;
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
            Debug.Log("clicked");
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
