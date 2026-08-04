using UnityEngine;
using UnityEngine.UI;

public class ButtonSound : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip clickSound;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(PlayClickSound);
    }

    void PlayClickSound()
    {
        audioSource.PlayOneShot(clickSound);
    }
}