using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Hace que un boton crezca y cambie de color cuando el raton pasa por encima.</summary>
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 escalaOriginal;
    private Vector3 escalaObjetivo;

    private Image imagen;

    [Header("Escala")]
    public float aumento = 1.08f;
    public float velocidad = 8f;

    [Header("Colores")]
    public Color colorNormal = Color.white;

    public Color colorHover = new Color(
        0.85f,
        0.30f,
        1f,
        1f);

    public float velocidadColor = 8f;

    private Color colorObjetivo;

    /// <summary>Guarda la escala y el color de partida del boton.</summary>
    void Start()
    {
        escalaOriginal = transform.localScale;
        escalaObjetivo = escalaOriginal;

        imagen = GetComponent<Image>();

        colorObjetivo = colorNormal;
    }

    /// <summary>Lleva la escala y el color hacia su objetivo poco a poco.</summary>
    void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            escalaObjetivo,
            velocidad * Time.deltaTime);

        imagen.color = Color.Lerp(
            imagen.color,
            colorObjetivo,
            velocidadColor * Time.deltaTime);
    }

    /// <summary>El raton entra: el boton crece y se tiñe.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        escalaObjetivo = escalaOriginal * aumento;
        colorObjetivo = colorHover;
    }

    /// <summary>El raton sale: el boton vuelve a como estaba.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        escalaObjetivo = escalaOriginal;
        colorObjetivo = colorNormal;
    }
}