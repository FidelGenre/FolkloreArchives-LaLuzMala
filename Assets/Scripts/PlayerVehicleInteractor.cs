// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PlayerVehicleInteractor.cs — subir/bajar del auto con E.
//  Al subir: apaga el movimiento a pie (MapExplorer + CharacterController),
//  oculta el cuerpo del jugador, lleva la cámara al asiento del auto y
//  activa el CarController. Mientras manejás, el mouse mueve la vista
//  (free-look acotado) sin girar el auto. E de nuevo = bajar al costado.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class PlayerVehicleInteractor : MonoBehaviour
    {
        public float enterRange = 3.5f;
        public float lookYawLimit = 120f;   // cuánto podés girar la vista adentro
        public float lookPitchLimit = 45f;
        public float lookSensitivity = 0.08f;

        CharacterController cc;
        MapExplorer explorer;
        Transform cam;
        Transform camParent;
        Vector3 camLocalPos;
        Quaternion camLocalRot;
        Renderer[] bodyRenderers;

        CarController car;   // auto en el que estoy; null = a pie
        float lookYaw, lookPitch;

        void Start()
        {
            cc = GetComponent<CharacterController>();
            explorer = GetComponent<MapExplorer>();
            var c = GetComponentInChildren<Camera>();
            if (c != null)
            {
                cam = c.transform;
                camParent = cam.parent;
                camLocalPos = cam.localPosition;
                camLocalRot = cam.localRotation;
            }
            // renderers del CUERPO (para ocultarlos al subir). El perro es hermano, no
            // se toca. La cámara/linterna no tienen renderer.
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || SettingsMenu.IsOpen) return;

            if (kb.eKey.wasPressedThisFrame)
            {
                if (car == null) TryEnter();
                else Exit();
            }

            // free-look mientras manejo (mueve la vista, no el auto)
            if (car != null && cam != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 d = mouse.delta.ReadValue() * lookSensitivity;
                    lookYaw = Mathf.Clamp(lookYaw + d.x, -lookYawLimit, lookYawLimit);
                    lookPitch = Mathf.Clamp(lookPitch - d.y, -lookPitchLimit, lookPitchLimit);
                    cam.localEulerAngles = new Vector3(lookPitch, lookYaw, 0f);
                }
            }
        }

        void TryEnter()
        {
            CarController best = null; float bestD = enterRange;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best == null) return;

            car = best;
            if (explorer != null) explorer.enabled = false;
            if (cc != null) cc.enabled = false;
            SetBodyVisible(false);

            Transform seat = car.driverSeat != null ? car.driverSeat : car.transform;
            cam.SetParent(seat, false);
            cam.localPosition = Vector3.zero;
            cam.localRotation = Quaternion.identity;
            lookYaw = 0f; lookPitch = 0f;
            car.driving = true;
        }

        void Exit()
        {
            var c = car; car = null;
            c.driving = false;

            // bajar al lado izquierdo del auto, apoyado en el piso
            Vector3 side = c.transform.position - c.transform.right * 1.6f + Vector3.up * 1.5f;
            if (Physics.Raycast(side + Vector3.up * 2f, Vector3.down, out var hit, 8f))
                side.y = hit.point.y + 0.1f;

            cam.SetParent(camParent, false);
            cam.localPosition = camLocalPos;
            cam.localRotation = camLocalRot;

            transform.position = side;
            SetBodyVisible(true);
            if (cc != null) cc.enabled = true;
            if (explorer != null) explorer.enabled = true;
        }

        void SetBodyVisible(bool v)
        {
            if (bodyRenderers == null) return;
            foreach (var r in bodyRenderers) if (r != null) r.enabled = v;
        }

        void OnGUI()
        {
            // prompt "E para subir" cuando estás cerca y a pie
            if (car != null) return;
            bool near = false;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
                if (Vector3.Distance(transform.position, c.transform.position) < enterRange) { near = true; break; }
            if (!near) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width / 2 - 130, Screen.height - 90, 260, 34), GUIContent.none);
            GUI.Label(new Rect(Screen.width / 2 - 130, Screen.height - 90, 260, 34), "[ E ]  Subir al auto", style);
        }
    }
}
