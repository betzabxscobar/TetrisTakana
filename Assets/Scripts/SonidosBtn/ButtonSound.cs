using UnityEngine;
using UnityEngine.UI;

/// <summary>Reproduce un sonido cada vez que se pulsa el boton.</summary>
public class ButtonSound : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip clickSound;

    /// <summary>Engancha el sonido al clic del boton.</summary>
    void Start()
    {
        GetComponent<Button>().onClick.AddListener(PlayClickSound);
    }

    /// <summary>Suena el clic.</summary>
    void PlayClickSound()
    {
        audioSource.PlayOneShot(clickSound);
    }
}