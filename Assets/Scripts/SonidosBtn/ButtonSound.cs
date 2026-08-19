using UnityEngine;
using UnityEngine.UI;
using TetrisTakana;

/// <summary>Reproduce un sonido cada vez que se pulsa el boton.</summary>
public class ButtonSound : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip clickSound;

    /// <summary>Engancha el sonido al clic del boton.</summary>
    void Start()
    {
        Button button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(PlayClickSound);
    }

    /// <summary>Suena el clic.</summary>
    void PlayClickSound()
    {
        if (clickSound == null)
            return;

        // En las escenas los botones no llevan un AudioSource propio. Usar el
        // gestor compartido evita una referencia nula que interrumpia el resto
        // de callbacks de onClick (incluido el de JUGAR).
        if (audioSource != null)
            audioSource.PlayOneShot(clickSound);
        else
            AudioManager.EnsureInstance().PlaySfx(clickSound);
    }
}
