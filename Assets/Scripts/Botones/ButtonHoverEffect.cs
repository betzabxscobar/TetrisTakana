using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    void Start()
    {
        escalaOriginal = transform.localScale;
        escalaObjetivo = escalaOriginal;

        imagen = GetComponent<Image>();

        colorObjetivo = colorNormal;
    }

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

    public void OnPointerEnter(PointerEventData eventData)
    {
        escalaObjetivo = escalaOriginal * aumento;
        colorObjetivo = colorHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        escalaObjetivo = escalaOriginal;
        colorObjetivo = colorNormal;
    }
}