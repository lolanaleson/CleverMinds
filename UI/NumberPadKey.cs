using UnityEngine;

public class NumberPadKey : MonoBehaviour
{
    public NumberPad pad;    // Asignar en Inspector
    public string symbol;    // "0".."9", "DEL", "CLR"

    public void OnPressed()
    {
        if (pad == null || string.IsNullOrEmpty(symbol)) return;
        switch (symbol)
        {
            case "DEL": pad.PressDelete(); break;
            case "CLR": pad.PressClear(); break;
            default: pad.PressDigit(symbol); break;
        }
    }
}
