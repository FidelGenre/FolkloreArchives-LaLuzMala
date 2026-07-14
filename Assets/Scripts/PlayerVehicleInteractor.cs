// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PlayerVehicleInteractor.cs — subir/bajar del auto con E.
//  E sube al ASIENTO MÁS CERCANO (conductor/acompañante/traseros):
//  se abre la puerta de ese asiento, la cámara ENTRA suave y la
//  puerta cierra. Solo manejás si te subís al asiento del conductor.
//  E de nuevo = se abre la puerta, bajás al costado y cierra.
//  Mientras manejás, el mouse mueve la vista (free-look acotado).
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class PlayerVehicleInteractor : MonoBehaviour
    {
        public float enterRange = 4.5f;
        public float lookYawLimit = 120f;
        public float lookPitchLimit = 45f;
        public float lookSensitivity = 0.08f;
        public float enterDuration = 0.6f;
        public float doorOpenDeg = 72f;

        CharacterController cc;
        MapExplorer explorer;
        Transform cam;
        Transform camParent;
        Vector3 camLocalPos;
        Quaternion camLocalRot;
        Renderer[] bodyRenderers;

        CarController car;      // auto en el que estoy; null = a pie
        Transform mySeat;       // asiento en el que estoy
        bool busy;
        float lookYaw, lookPitch;
        readonly Dictionary<Transform, Quaternion> doorClosed = new Dictionary<Transform, Quaternion>();

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

        static Transform[] Seats(CarController c) => new[] { c.driverSeat, c.frontPassenger, c.rearLeft, c.rearRight };

        void TryEnter()
        {
            // Elijo la PUERTA más cercana a donde estoy; de ahí sale el asiento (así coinciden).
            CarController bestCar = null; Transform bestDoor = null; float bestD = enterRange;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(transform.position, c.transform.position) > enterRange + 4f) continue;
                if (c.doors == null) continue;
                foreach (var d in c.doors)
                {
                    if (d == null) continue;
                    float dist = Vector3.Distance(transform.position, d.position);
                    if (dist < bestD) { bestD = dist; bestCar = c; bestDoor = d; }
                }
            }
            if (bestCar == null)
            {
                // fallback (auto sin puertas): asiento más cercano
                Transform bestSeat = null; float sd = enterRange + 1.5f;
                foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
                    foreach (var s in Seats(c))
                    {
                        if (s == null) continue;
                        float d = Vector3.Distance(transform.position, s.position);
                        if (d < sd) { sd = d; bestSeat = s; bestCar = c; }
                    }
                if (bestCar != null) StartCoroutine(EnterRoutine(bestCar, bestSeat, null));
                return;
            }
            Transform seat = NearestSeat(bestCar, bestDoor.position);
            StartCoroutine(EnterRoutine(bestCar, seat, bestDoor));
        }

        Transform NearestSeat(CarController c, Vector3 to)
        {
            Transform best = null; float bd = float.MaxValue;
            foreach (var s in Seats(c))
            {
                if (s == null) continue;
                float d = Vector3.Distance(s.position, to);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        IEnumerator EnterRoutine(CarController c, Transform seat, Transform door)
        {
            busy = true;
            if (explorer != null) explorer.enabled = false;
            if (cc != null) cc.enabled = false;
            SetBodyVisible(false);

            if (door == null) door = NearestDoor(c, seat);
            yield return AnimateDoor(c, door, true, 0.32f);

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

            yield return AnimateDoor(c, door, false, 0.32f);

            car = c; mySeat = seat;
            c.driving = (seat == c.driverSeat);   // solo manejás en el asiento del conductor
            busy = false;
        }

        IEnumerator ExitRoutine()
        {
            busy = true;
            var c = car; var seat = mySeat; car = null; mySeat = null; c.driving = false;

            Transform door = NearestDoor(c, seat);
            yield return AnimateDoor(c, door, true, 0.30f);

            // ubicar al jugador al lado de la puerta (cuerpo oculto todavía)
            Vector3 sideDir = (seat.position - c.transform.position); sideDir.y = 0f;
            if (sideDir.sqrMagnitude < 0.01f) sideDir = -c.transform.right;
            Vector3 side = c.transform.position + sideDir.normalized * 1.8f + Vector3.up * 1.5f;
            if (Physics.Raycast(side + Vector3.up * 2f, Vector3.down, out var hit, 8f))
                side.y = hit.point.y + 0.1f;
            transform.position = side;

            // deslizar la cámara del asiento hasta el ojo del jugador afuera (salida SUAVE)
            cam.SetParent(null, true);
            Vector3 p0 = cam.position; Quaternion r0 = cam.rotation;
            Vector3 targetPos = camParent.TransformPoint(camLocalPos);
            Quaternion targetRot = camParent.rotation * camLocalRot;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, enterDuration);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cam.position = Vector3.Lerp(p0, targetPos, e);
                cam.rotation = Quaternion.Slerp(r0, targetRot, e);
                yield return null;
            }
            cam.SetParent(camParent, false);
            cam.localPosition = camLocalPos;
            cam.localRotation = camLocalRot;

            SetBodyVisible(true);
            if (cc != null) cc.enabled = true;
            if (explorer != null) explorer.enabled = true;

            yield return AnimateDoor(c, door, false, 0.30f);
            busy = false;
        }

        Transform NearestDoor(CarController c, Transform seat)
        {
            if (c.doors == null || seat == null) return null;
            Transform best = null; float bd = float.MaxValue;
            foreach (var d in c.doors)
            {
                if (d == null) continue;
                float dd = Vector3.Distance(d.position, seat.position);
                if (dd < bd) { bd = dd; best = d; }
            }
            return best;
        }

        IEnumerator AnimateDoor(CarController c, Transform door, bool open, float dur)
        {
            if (door == null) yield break;
            if (!doorClosed.ContainsKey(door)) doorClosed[door] = door.localRotation;
            Quaternion closed = doorClosed[door];
            // sentido de apertura según el lado de la puerta (izq/der)
            float sign = c.transform.InverseTransformPoint(door.position).x < 0f ? 1f : -1f;
            Quaternion openRot = closed * Quaternion.Euler(0f, sign * doorOpenDeg, 0f);
            Quaternion from = door.localRotation;
            Quaternion to = open ? openRot : closed;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, dur);
                door.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            door.localRotation = to;
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
                if (Vector3.Distance(transform.position, c.transform.position) < enterRange + 3f) { near = true; break; }
            if (!near) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width / 2 - 150, Screen.height - 90, 300, 34), GUIContent.none);
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height - 90, 300, 34), "[ E ]  Subir (asiento más cercano)", style);
        }
    }
}
