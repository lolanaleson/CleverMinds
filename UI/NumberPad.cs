using UnityEngine;
using TMPro;

/// <summary>
/// Controlador del teclado numérico. Se conecta con el DemographicsController para escribir la edad.
/// </summary>
public class NumberPad : MonoBehaviour
{
    public DemographicsController controller; // Asignar en Inspector
    public TMP_InputField targetInput;        // El campo de edad (lo pondremos ReadOnly=true)
    public int maxDigits = 3;                 // Máximo 3 dígitos

    private void Start()
    {
        if (targetInput != null) targetInput.readOnly = true; // Importante: que nadie teclee con el teclado físico
    }

    public void PressDigit(string digit)
    {
        if (controller == null || targetInput == null) return;
        if (string.IsNullOrEmpty(digit)) return;
        if (targetInput.text.Length >= maxDigits) return;     // Limita a 3 caracteres

        // Evita 0 inicial si lo deseas (opcional). Si no lo quieres, comenta este bloque.
        if (targetInput.text.Length == 0 && digit == "0") return;

        targetInput.text += digit;
        controller.OnAgeChanged(targetInput.text); // revalida
    }

    public void PressDelete()
    {
        if (controller == null || targetInput == null) return;
        if (targetInput.text.Length == 0) return;
        targetInput.text = targetInput.text.Substring(0, targetInput.text.Length - 1);
        controller.OnAgeChanged(targetInput.text); // revalida
    }

    public void PressClear()
    {
        if (controller == null || targetInput == null) return;
        targetInput.text = "";
        controller.OnAgeChanged(targetInput.text); // revalida
    }
}
