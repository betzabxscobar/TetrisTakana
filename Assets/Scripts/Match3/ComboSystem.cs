using System;
using UnityEngine;

namespace TetrisTakana.Match3
{
    public class ComboSystem : MonoBehaviour
    {
        public int CurrentCombo { get; private set; }
        public event Action<int> ComboChanged;

        public int RegisterCascade()
        {
            CurrentCombo++;
            ComboChanged?.Invoke(CurrentCombo);
            return CurrentCombo;
        }

        public void ResetCombo()
        {
            if (CurrentCombo == 0)
                return;

            CurrentCombo = 0;
            ComboChanged?.Invoke(CurrentCombo);
        }
    }
}
