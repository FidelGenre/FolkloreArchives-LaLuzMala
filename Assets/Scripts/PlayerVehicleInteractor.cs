// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PlayerVehicleInteractor.cs — subir/bajar del auto con E.
//  Al subir: se abre la PUERTA del conductor, la cámara ENTRA suave
//  hasta el asiento (no teletransporta), se apaga el movimiento a pie
//  y se oculta el cuerpo. Al manejar, el mouse mueve la vista adentro.
//  E de nuevo = se abre la puerta, bajás al costado y cierra.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class PlayerVehicleInteractor : MonoBehaviour
    {
        public float enterRange = 3.5f;
        public float lookYawLimit = 120f;
        public float lookPitchLimit = 45f;
        public float lookSensitivity = 0.08f;
        public float enterDuration = 0.6f;

        CharacterController cc;
        MapExplorer explorer;
        Transform cam;
        Transform camParent;
        Vector3 camLocalPos;
        Quaternion camLocalRot;
        Renderer[] bodyRenderers;

        CarController car;      // auto en el que estoy; null = a pie
        bool busy;             // entrando/bajando (bloquea input)
        float lookYaw, lookPitch;
        float doorAngle;

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
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || SettingsMenu.IsOpen || busy) return;

            if (kb.eKey.wasPressedThisFrame)
            {
                if (car == null) TryEnter();
                else StartCoroutine(ExitRoutine());
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
            if (best != null) StartCoroutine(EnterRoutine(best));
        }

        IEnumerator EnterRoutine(CarController c)
        {
            busy = true;
            if (explorer != null) explorer.enabled = false;
            if (cc != null) cc.enabled = false;
            SetBodyVisible(false);

            yield return AnimateDoor(c, c.doorOpenAngle, 0.32f);   // abrir puerta

            // deslizar la cámara desde donde está hasta el asiento
            Transform seat = c.driverSeat != null ? c.driverSeat : c.transform;
            cam.SetParent(null, true);
            Vector3 p0 = cam.position; Quaternion r0 = cam.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, enterDuration);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cam.position = Vector3.Lerp(p0, seat.position, e);
                cam.rotation = Quaternion.Slerp(r0, seat.rotation, e);
                yield return null;
            }
            cam.SetParent(seat, false);
            cam.localPosition = Vector3.zero;
            cam.localRotation = Quaternion.identity;
            lookYaw = 0f; lookPitch = 0f;

            yield return AnimateDoor(c, 0f, 0.32f);                 // cerrar puerta

            car = c; c.driving = true;
            busy = false;
        }

        IEnumerator ExitRoutine()
        {
            busy = true;
            var c = car; car = null; c.driving = false;

            yield return AnimateDoor(c, c.doorOpenAngle, 0.30f);    // abrir puerta

            cam.SetParent(camParent, false);
            cam.localPosition = camLocalPos;
            cam.localRotation = camLocalRot;

            // bajar al lado izquierdo, apoyado en el piso
            Vector3 side = c.transform.position - c.transform.right * 1.8f + Vector3.up * 1.5f;
            if (Physics.Raycast(side + Vector3.up * 2f, Vector3.down, out var hit, 8f))
                side.y = hit.point.y + 0.1f;
            transform.position = side;

            SetBodyVisible(true);
            if (cc != null) cc.enabled = true;
            if (explorer != null) explorer.enabled = true;

            yield return AnimateDoor(c, 0f, 0.30f);                 // cerrar puerta
            busy = false;
        }

        IEnumerator AnimateDoor(CarController c, float targetAngle, float dur)
        {
            if (c.driverDoor == null) yield break;
            float from = doorAngle;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, dur);
                doorAngle = Mathf.Lerp(from, targetAngle, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                c.driverDoor.localRotation = Quaternion.Euler(0f, doorAngle, 0f);
                yield return null;
            }
            doorAngle = targetAngle;
            c.driverDoor.localRotation = Quaternion.Euler(0f, doorAngle, 0f);
        }

        void SetBodyVisible(bool v)
        {
            if (bodyRenderers == null) return;
            foreach (var r in bodyRenderers) if (r != null) r.enabled = v;
        }

        void OnGUI()
        {
            if (car != null || busy) return;
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
