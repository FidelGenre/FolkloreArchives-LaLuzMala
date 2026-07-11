// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PartyController.cs — decide quién se controla.
//  MODO SOLO (por ahora): controlás la PERSONA (1ª persona) y el
//  perro te sigue por IA. Con la tecla de cambio (G) tomás el
//  control del PERRO (3ª persona) y la persona queda quieta.
//  La cámara activa y el AudioListener se intercambian solos:
//  se apaga el GameObject de la cámara inactiva (y con él su
//  AudioListener), así nunca hay dos listeners a la vez.
//
//  CO-OP (teclado compartido) vendrá después sobre esta misma base:
//  persona = WASD+mouse (J1), perro = flechas (J2), pantalla dividida.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class PartyController : MonoBehaviour
    {
        [Header("Refs (las asigna TestPlayerBuilder)")]
        public MapExplorer person;
        public DogController dog;
        public Camera personCam;
        public Camera dogCam;

        [Header("Control")]
        public Key switchKey = Key.G;   // tomar/soltar el control del perro
        public Key stayKey   = Key.E;   // (offline) que el perro se quede quieto / vuelva a seguir

        bool controllingDog;
        bool dogStay;   // el perro está esperando quieto (no te sigue)

        void Start() => Apply();

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || SettingsMenu.IsOpen) return;

            if (kb[switchKey].wasPressedThisFrame)
            {
                controllingDog = !controllingDog;
                Apply();
            }
            // E: alterna quedarse/seguir (solo cuando controlás la persona)
            else if (!controllingDog && kb[stayKey].wasPressedThisFrame)
            {
                dogStay = !dogStay;
                Apply();
            }
        }

        void Apply()
        {
            // persona: activa solo si NO controlás al perro
            if (person != null) person.enabled = !controllingDog;
            // perro: Player si lo controlás; si no, Idle (quieto) si le dijiste que espere,
            // o Follow (te sigue).
            if (dog != null)
                dog.mode = controllingDog ? DogController.Mode.Player
                         : dogStay        ? DogController.Mode.Idle
                                          : DogController.Mode.Follow;

            // cámara + AudioListener: solo una activa
            Camera on  = controllingDog ? dogCam : personCam;
            Camera off = controllingDog ? personCam : dogCam;
            if (off != null) off.gameObject.SetActive(false);
            if (on  != null) on.gameObject.SetActive(true);

            Cursor.lockState = CursorLockMode.Locked;
        }

        GUIStyle _hint;
        void OnGUI()
        {
            if (controllingDog) return;   // el hint del perro solo cuando controlás la persona
            if (_hint == null) _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            string s = dogStay ? "<color=orange>Rufus: QUIETO</color>  (E: que te siga)"
                               : "Rufus: te sigue  (E: quedarse | G: controlarlo)";
            GUI.Label(new Rect(12f, 44f, 420f, 22f), s, _hint);
        }
    }
}
