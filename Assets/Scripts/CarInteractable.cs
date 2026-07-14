// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarInteractable.cs — marca una parte del auto (puerta o asiento)
//  para que la MIRA invisible (raycast al centro de la pantalla) la
//  detecte. 'part' es la puerta (pivote que gira) o el asiento (ancla).
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class CarInteractable : MonoBehaviour
    {
        public CarController car;
        public Transform part;   // puerta (pivote) o asiento (ancla)
        public bool isSeat;      // true = asiento, false = puerta
    }
}
