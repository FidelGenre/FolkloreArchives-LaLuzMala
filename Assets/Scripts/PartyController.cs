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

        // true si estás controlando al perro -> el auto no debe leer WASD (lo mira CarController).
        public static bool DogControlled;

        // true durante un diálogo con zoom (FocusSay): persona y perro NO mueven cámara ni caminan
        // (la cámara la maneja la secuencia, apuntando al que habla).
        public static bool CinematicLock;

        // owner: "cambie al perro y ahora puede abrir y cerrar puertas... y no se esta
        // podiendo subir" -- Apply() apagaba el MapExplorer de la persona al cambiar al
        // perro, pero NUNCA su PlayerVehicleInteractor: ese componente seguía activo
        // todo el tiempo (Update/OnGUI corren en CUALQUIER script enabled, sin importar
        // "quién controlás"), dibujando su propio cartel de puerta y compitiendo por la
        // tecla E contra el interactor del perro. Cacheados acá para poder
        // habilitar/deshabilitar el que corresponda en Apply().
        PlayerVehicleInteractor _personInteractor, _dogInteractor;

        void Start()
        {
            if (person != null) _personInteractor = person.GetComponent<PlayerVehicleInteractor>();
            if (dog != null) _dogInteractor = dog.GetComponent<PlayerVehicleInteractor>();
            // owner: el perro tiene que tener el MISMO crosshair que el humano. La cámara del perro
            // se construía sin él -> lo agrego en runtime a las dos (sin duplicar) por si falta.
            EnsureCrosshair(personCam);
            EnsureCrosshair(dogCam);
            Apply();
        }

        static void EnsureCrosshair(Camera cam)
        {
            if (cam != null && cam.GetComponent<Crosshair>() == null)
                cam.gameObject.AddComponent<Crosshair>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || SettingsMenu.IsOpen) return;

            if (kb[switchKey].wasPressedThisFrame)
            {
                controllingDog = !controllingDog;
                Apply();
            }
            // E: alterna quedarse/seguir -- owner: "solo si tengo al perro cerca y apuntándolo"
            // (así la E no choca con abrir puertas u otras interacciones).
            else if (!controllingDog && kb[stayKey].wasPressedThisFrame && LookingAtDog())
            {
                dogStay = !dogStay;
                Apply();
            }
        }

        // ¿estoy cerca del perro y apuntándolo? (para que E de "quedate/seguime" no se
        // dispare desde cualquier lado ni pise otras interacciones con E).
        bool LookingAtDog()
        {
            if (dog == null || personCam == null) return false;
            Vector3 to = dog.transform.position - personCam.transform.position;
            if (to.magnitude > 4.5f) return false;                          // cerca
            return Vector3.Angle(personCam.transform.forward, to) < 35f;    // apuntándolo
        }

        void Apply()
        {
            // owner: "controlando al perro también se mueve el auto" -> el auto NO debe leer WASD
            // mientras manejás al perro. Expongo el estado para que CarController lo mire.
            DogControlled = controllingDog;
            // persona: activa solo si NO controlás al perro
            if (person != null) person.enabled = !controllingDog;
            // mismo criterio para el interactor del auto de cada uno -- solo el que
            // controlás activamente puede subir/bajar/tocar puertas.
            if (_personInteractor != null) _personInteractor.enabled = !controllingDog;
            if (_dogInteractor != null) _dogInteractor.enabled = controllingDog;
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

        // owner: "moviendo al perro en la YPF también se mueve el personaje principal".
        // Si algo (la secuencia de apertura / ExitRoutine al bajar del auto) reactiva el
        // control de la PERSONA mientras controlás al perro, acá lo re-apago cada frame
        // (y viceversa). Así nunca se controlan los dos a la vez.
        void LateUpdate()
        {
            if (person != null && person.enabled == controllingDog) person.enabled = !controllingDog;
            if (_personInteractor != null && _personInteractor.enabled == controllingDog) _personInteractor.enabled = !controllingDog;
            if (_dogInteractor != null && _dogInteractor.enabled != controllingDog) _dogInteractor.enabled = controllingDog;
        }

        GUIStyle _hint;
        void OnGUI()
        {
            if (_hint == null) _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            string s;
            if (controllingDog)
                s = "Controlás a Rufus  (<color=yellow>B: ladrar</color> | G: volver a la persona)";
            else
                s = dogStay ? "<color=orange>Rufus: QUIETO</color>  (E: que te siga)"
                            : "Rufus: te sigue  (E: quedarse | G: controlarlo)";
            GUI.Label(new Rect(12f, 44f, 480f, 22f), s, _hint);
        }
    }
}
