// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PlayerVehicleInteractor.cs — subir/bajar del auto, MANUAL.
//    E  → si la puerta más cercana está CERRADA: la abre (no te sienta).
//         si está ABIERTA: te subís a ESE asiento (despacio).
//         si ya estás sentado: te bajás (despacio).
//    Q  → abre/cierra a mano la puerta más cercana (para cerrarla al subir).
//  Solo manejás si te subís del lado del conductor. Mientras manejás,
//  el mouse mueve la vista (free-look acotado).
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
        Transform cam, camParent;
        Vector3 camLocalPos;
        Quaternion camLocalRot;
        Renderer[] bodyRenderers;

        CarController car;      // auto en el que estoy; null = a pie
        Transform mySeat, myDoor;
        bool busy;
        float lookYaw, lookPitch;
        readonly Dictionary<Transform, Quaternion> doorClosed = new Dictionary<Transform, Quaternion>();
        readonly HashSet<Transform> openDoors = new HashSet<Transform>();

        void Start()
        {
            cc = GetComponent<CharacterController>();
            explorer = GetComponent<MapExplorer>();
            var c = GetComponentInChildren<Camera>();
            if (c != null) { cam = c.transform; camParent = cam.parent; camLocalPos = cam.localPosition; camLocalRot = cam.localRotation; }
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || SettingsMenu.IsOpen || busy) return;

            // Q: abrir/cerrar a mano la puerta más cercana
            if (kb.qKey.wasPressedThisFrame)
            {
                Vector3 from = car != null && cam != null ? cam.position : transform.position;
                var (dc, dd) = FindNearestDoor(from);
                if (dd != null) StartCoroutine(SetDoor(dc, dd, !openDoors.Contains(dd)));
            }

            // E: abrir puerta / sentarse / bajarse
            if (kb.eKey.wasPressedThisFrame)
            {
                if (car != null) StartCoroutine(ExitRoutine());
                else
                {
                    var (dc, dd) = FindNearestDoor(transform.position);
                    if (dd != null)
                    {
                        if (openDoors.Contains(dd))
                            StartCoroutine(SitRoutine(dc, NearestSeat(dc, dd.position), dd));   // abierta → subir
                        else
                            StartCoroutine(SetDoor(dc, dd, true));                              // cerrada → abrir
                    }
                }
            }

            // free-look mientras manejo/voy de acompañante
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

        (CarController, Transform) FindNearestDoor(Vector3 from)
        {
            CarController bc = null; Transform bd = null; float best = enterRange;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                if (c.doors == null) continue;
                if (Vector3.Distance(from, c.transform.position) > enterRange + 5f) continue;
                foreach (var d in c.doors)
                {
                    if (d == null) continue;
                    float dist = Vector3.Distance(from, d.position);
                    if (dist < best) { best = dist; bc = c; bd = d; }
                }
            }
            return (bc, bd);
        }

        Transform NearestSeat(CarController c, Vector3 to)
        {
            Transform best = null; float bd = float.MaxValue;
            foreach (var s in Seats(c)) { if (s == null) continue; float d = Vector3.Distance(s.position, to); if (d < bd) { bd = d; best = s; } }
            return best;
        }

        IEnumerator SetDoor(CarController c, Transform door, bool open)
        {
            busy = true;
            yield return AnimateDoor(c, door, open, 0.35f);
            if (open) openDoors.Add(door); else openDoors.Remove(door);
            busy = false;
        }

        IEnumerator SitRoutine(CarController c, Transform seat, Transform door)
        {
            busy = true;
            if (explorer != null) explorer.enabled = false;
            if (cc != null) cc.enabled = false;
            SetBodyVisible(false);

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
            cam.localPosition = Vector3.zero; cam.localRotation = Quaternion.identity;
            lookYaw = 0f; lookPitch = 0f;

            car = c; mySeat = seat; myDoor = door;
            c.driving = (seat == c.driverSeat);   // solo manejás en el asiento del conductor
            busy = false;
        }

        IEnumerator ExitRoutine()
        {
            busy = true;
            var c = car; var seat = mySeat; var door = myDoor;
            car = null; mySeat = null; myDoor = null; c.driving = false;

            // si la puerta está cerrada, abrirla para poder bajar
            if (door != null && !openDoors.Contains(door)) { yield return AnimateDoor(c, door, true, 0.30f); openDoors.Add(door); }

            // ubicar al jugador al lado de la puerta (cuerpo oculto)
            Vector3 sideDir = (seat.position - c.transform.position); sideDir.y = 0f;
            if (sideDir.sqrMagnitude < 0.01f) sideDir = -c.transform.right;
            Vector3 side = c.transform.position + sideDir.normalized * 1.8f + Vector3.up * 1.5f;
            if (Physics.Raycast(side + Vector3.up * 2f, Vector3.down, out var hit, 8f)) side.y = hit.point.y + 0.1f;
            transform.position = side;

            // deslizar la cámara del asiento hasta el ojo del jugador afuera (SUAVE)
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
            cam.localPosition = camLocalPos; cam.localRotation = camLocalRot;

            SetBodyVisible(true);
            if (cc != null) cc.enabled = true;
            if (explorer != null) explorer.enabled = true;
            busy = false;   // la puerta queda ABIERTA; se cierra con Q
        }

        IEnumerator AnimateDoor(CarController c, Transform door, bool open, float dur)
        {
            if (door == null) yield break;
            if (!doorClosed.ContainsKey(door)) doorClosed[door] = door.localRotation;
            Quaternion closed = doorClosed[door];
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
            if (busy) return;
            string msg;
            if (car != null) msg = "[ E ] Bajar    [ Q ] Puerta";
            else
            {
                var (_, dd) = FindNearestDoor(transform.position);
                if (dd == null) return;
                msg = openDoors.Contains(dd) ? "[ E ] Subir    [ Q ] Cerrar puerta" : "[ E ] Abrir puerta    [ Q ] Puerta";
            }
            var style = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width / 2 - 190, Screen.height - 90, 380, 32), GUIContent.none);
            GUI.Label(new Rect(Screen.width / 2 - 190, Screen.height - 90, 380, 32), msg, style);
        }
    }
}
