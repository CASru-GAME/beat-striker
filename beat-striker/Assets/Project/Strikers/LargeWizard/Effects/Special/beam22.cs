using UnityEngine;

public class beam22 : MonoBehaviour
{
      [SerializeField] AudioClip audioClip;
    [SerializeField, Min(0f)] float audioPlayDelaySeconds = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (audioPlayDelaySeconds <= 0f)
        {
            audioClip.PlayAtApp(transform.position);
            return;
        }

        StartCoroutine(PlayAudioAfterDelay());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    System.Collections.IEnumerator PlayAudioAfterDelay()
    {
        yield return new WaitForSeconds(audioPlayDelaySeconds);
        audioClip.PlayAtApp(transform.position);
    }
}
