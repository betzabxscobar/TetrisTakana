using System;
using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>Lleva la cuenta de cuantas combinaciones se encadenan seguidas.</summary>
    public class ComboSystem : MonoBehaviour
    {
        public int CurrentCombo { get; private set; }
        public event Action<int> ComboChanged;

        /// <summary>Apunta un eslabon mas de la cadena y devuelve el combo actual.</summary>
        public int RegisterCascade()
        {
            CurrentCombo++;
            ComboChanged?.Invoke(CurrentCombo);
            return CurrentCombo;
        }

        /// <summary>Corta la racha y la deja a cero.</summary>
        public void ResetCombo()
        {
            if (CurrentCombo == 0)
                return;

            CurrentCombo = 0;
            ComboChanged?.Invoke(CurrentCombo);
        }
    }
}
