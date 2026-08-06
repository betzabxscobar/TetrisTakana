using System.Collections;
using UnityEngine;

/// <summary>Hace que una tarjeta parpadee suavemente para llamar la atencion.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class ParpadeoTarjeta : MonoBehaviour
{
    [Header("Transparencia")]
    [SerializeField, Range(0f, 1f)]
    private float alphaMinimo = 0.15f;

    [SerializeField, Range(0f, 1f)]
    private float alphaMaximo = 1f;

    [Header("Tiempo entre fallos")]
    [SerializeField]
    private Vector2 esperaEntreParpadeos = new Vector2(1f, 4f);

    [Header("Duración del titileo")]
    [SerializeField]
    private Vector2 duracionTitileo = new Vector2(0.03f, 0.12f);

    [SerializeField, Min(1)]
    private int cantidadMinimaTitileos = 1;

    [SerializeField, Min(1)]
    private int cantidadMaximaTitileos = 4;

    private CanvasGroup canvasGroup;
    private Coroutine rutinaParpadeo;

    /// <summary>Coge el CanvasGroup y parte de la opacidad maxima.</summary>
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = alphaMaximo;
    }

    /// <summary>Empieza a parpadear al activarse.</summary>
    private void OnEnable()
    {
        IniciarParpadeo();
    }

    /// <summary>Deja de parpadear al desactivarse.</summary>
    private void OnDisable()
    {
        DetenerParpadeo();
    }

    /// <summary>Arranca el parpadeo si no estaba ya en marcha.</summary>
    public void IniciarParpadeo()
    {
        if (rutinaParpadeo != null)
            return;

        rutinaParpadeo = StartCoroutine(ParpadearComoFoco());
    }

    /// <summary>Corta el parpadeo y deja la tarjeta opaca.</summary>
    public void DetenerParpadeo()
    {
        if (rutinaParpadeo != null)
        {
            StopCoroutine(rutinaParpadeo);
            rutinaParpadeo = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = alphaMaximo;
    }

    /// <summary>Sube y baja la opacidad sin parar, como un foco.</summary>
    private IEnumerator ParpadearComoFoco()
    {
        while (true)
        {
            canvasGroup.alpha = alphaMaximo;

            float espera = Random.Range(
                esperaEntreParpadeos.x,
                esperaEntreParpadeos.y
            );

            yield return new WaitForSecondsRealtime(espera);

            int cantidad = Random.Range(
                cantidadMinimaTitileos,
                cantidadMaximaTitileos + 1
            );

            for (int i = 0; i < cantidad; i++)
            {
                canvasGroup.alpha = Random.Range(alphaMinimo, 0.55f);

                yield return new WaitForSecondsRealtime(
                    Random.Range(duracionTitileo.x, duracionTitileo.y)
                );

                canvasGroup.alpha = Random.Range(0.75f, alphaMaximo);

                yield return new WaitForSecondsRealtime(
                    Random.Range(0.02f, 0.15f)
                );
            }

            canvasGroup.alpha = alphaMaximo;
        }
    }
}