using UnityEngine;
using UnityEngine.UI;
public class Stageselectbutton : MonoBehaviour
{
     Botan botan;
    public RawImage image;
    public AudioClip hoverSound;
    AudioSource audioSource;
    public Panel panel; // Panel参照
    public enum MoveType { None, Right, Left }
    public MoveType moveType = MoveType.None;
    public GameObject PopupPanel;
    private static bool isPopupShown = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        botan = GetComponent<Botan>();
        audioSource = GetComponent<AudioSource>();

        image.color = Color.gray;
        botan.onHover += (e) => {
            if (isPopupShown) return;
            image.color = Color.white;
            Debug.Log("hovered");
            if (hoverSound != null && audioSource != null) {
                audioSource.PlayOneShot(hoverSound);
            }
             if(panel != null) {
                if (moveType == MoveType.Right) panel.MoveRight();
                else if (moveType == MoveType.Left) panel.MoveLeft();
            }
        };
        botan.onClick += (e) => {
            Debug.Log("clicked");
            if (PopupPanel != null) {
                PopupPanel.SetActive(true);
                isPopupShown = true;
            }
        };
        botan.onHoverExit += (e) => {
            if (isPopupShown) return;
            image.color = Color.gray;
            Debug.Log("hover exited");
        };
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
