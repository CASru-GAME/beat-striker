using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;

public class Backbutton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public string previousSceneName = "TitleScene";
    public AudioClip clickSound;
    AudioSource audioSource;
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null && audioSource != null)
         {
            audioSource.PlayOneShot(clickSound);
             StartCoroutine(GoToSceneAfterSound());
        }
        else
        {
            SceneManager.LoadScene(previousSceneName);
        }
    }
    IEnumerator GoToSceneAfterSound()
    {
        yield return new WaitForSeconds(0.2f);
        SceneManager.LoadScene(previousSceneName);
    }

    public void OnPointerEnter(PointerEventData eventData) {

    }

    public void OnPointerExit(PointerEventData eventData)
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
