// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PlayerVehicleInteractor.cs — subir/bajar del auto, MANUAL, con E.
//    Afuera, cerca de una PUERTA:  E abre / cierra esa puerta.
//    Afuera, PEGADO a un asiento (puerta abierta):  E te sienta.
//    Sentado con la puerta ABIERTA:  E cierra la puerta.
//    Sentado con la puerta cerrada:  E te baja (abre y desliza afuera).
//  Solo manejás desde el asiento del conductor. Mouse = free-look.
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class PlayerVehicleInteractor : MonoBehaviour
    {
        public float doorRange = 3.5f;   // distancia para abrir/cerrar una puerta
        public float sitRange  = 1.8f;   // distancia (chica) para sentarte: hay que estar PEGADO
        public float lookYawLimit = 120f, lookPitchLimit = 45f, lookSensitivity = 0.08f;
        public float enterDuration = 0.6f;
        public float doorOpenDeg = 72f;

        CharacterController cc;
        MapExplorer explorer;
        Transform cam, camParent;
        Vector3 camLocalPos; Quaternion camLocalRot;
        Renderer[] bodyRenderers;

        CarController car;      // null = a pie
        Transform mySeat, myDoor;
        CarInteractable currentTarget;   // lo que apunta la mira este frame
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

            currentTarget = RaycastTarget();    // la MIRA (centro de pantalla), una vez por frame

            if (kb.eKey.wasPressedThisFrame)
            {
                var target = currentTarget;
                if (car != null)   // sentado
                {
                    if (target != null && !target.isSeat && target.part == myDoor)
                        StartCoroutine(SetDoor(car, myDoor, !openDoors.Contains(myDoor))); // apunto mi puerta → abrir/cerrar
                    else
                        StartCoroutine(ExitRoutine());                                     // apunto afuera → bajar
                }
                else if (target != null)   // a pie, apuntando algo del auto
                {
                    if (target.isSeat)
                        StartCoroutine(SitRoutine(target.car, target.part, NearestDoor(target.car, target.part.position))); // apunto el asiento → subir
                    else
                        StartCoroutine(SetDoor(target.car, target.part, !openDoors.Contains(target.part))); // puerta → abrir/cerrar
                }
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

        // MIRA invisible: qué parte del auto apunta el centro de la pantalla.
        CarInteractable RaycastTarget()
        {
            if (cam == null) return null;
            var hits = Physics.RaycastAll(cam.position, cam.forward, 4.5f, ~0, QueryTriggerInteraction.Collide);
            CarInteractable best = null; float bd = float.MaxValue;
            foreach (var h in hits)
            {
                var ci = h.collider.GetComponentInParent<CarInteractable>();
                if (ci != null && h.distance < bd) { bd = h.distance; best = ci; }
            }
            return best;
        }

        static Transform[] Seats(CarController c) => new[] { c.driverSeat, c.frontPassenger, c.rearLeft, c.rearRight };

        (CarController, Transform) FindNearestDoor(Vector3 from, float range)
        {
            CarController bc = null; Transform bd = null; float best = range;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                if (c.doors == null) continue;
                foreach (var d in c.doors)
                {
                    if (d == null) continue;
                    float dist = Vector3.Distance(from, d.position);
                    if (dist < best) { best = dist; bc = c; bd = d; }
                }
            }
            return (bc, bd);
        }

        Transform NearestDoor(CarController c, Vector3 to)
        {
            if (c.doors == null) return null;
            Transform best = null; float bd = float.MaxValue;
            foreach (var d in c.doors) { if (d == null) continue; float dd = Vector3.Distance(d.position, to); if (dd < bd) { bd = dd; best = d; } }
            return best;
        }

        // asiento PEGADO (dentro de sitRange) cuya puerta esté ABIERTA
        (Transform, Transform, CarController) NearestOpenSeat()
        {
            Transform bs = null, bd = null; CarController bc = null; float best = sitRange;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
                foreach (var s in Seats(c))
                {
                    if (s == null) continue;
                    float d = Vector3.Distance(transform.position, s.position);
                    if (d < best)
                    {
                        Transform door = NearestDoor(c, s.position);
                        if (door != null && openDoors.Contains(door)) { best = d; bs = s; bd = door; bc = c; }
                    }
                }
            return (bs, bd, bc);
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

            yield return Glide(cam, seat.position, seat.rotation);
            cam.SetParent(seat, false);
            cam.localPosition = Vector3.zero; cam.localRotation = Quaternion.identity;
            lookYaw = 0f; lookPitch = 0f;

            car = c; mySeat = seat; myDoor = door;
            c.driving = (seat == c.driverSeat);
            busy = false;
        }

        IEnumerator ExitRoutine()
        {
            busy = true;
            var c = car; var seat = mySeat; var door = myDoor;
            car = null; mySeat = null; myDoor = null; c.driving = false;

            if (door != null && !openDoors.Contains(door)) { yield return AnimateDoor(c, door, true, 0.30f); openDoors.Add(door); }

            // posición de bajada: al costado del auto, sobre el PISO (ignorando el propio auto)
            Vector3 sideDir = (seat.position - c.transform.position); sideDir.y = 0f;
            if (sideDir.sqrMagnitude < 0.01f) sideDir = -c.transform.right;
            Vector3 side = c.transform.position + sideDir.normalized * 2.2f;
            side.y = GroundYIgnoring(c, side) + 0.05f;
            transform.position = side;

            // deslizar la cámara del asiento hasta el ojo del jugador (SUAVE)
            Vector3 targetPos = camParent.TransformPoint(camLocalPos);
            Quaternion targetRot = camParent.rotation * camLocalRot;
            yield return Glide(cam, targetPos, targetRot);
            cam.SetParent(camParent, false);
            cam.localPosition = camLocalPos; cam.localRotation = camLocalRot;

            SetBodyVisible(true);
            if (cc != null) cc.enabled = true;
            if (explorer != null) explorer.enabled = true;
            busy = false;
        }

        // desliza un transform (la cámara) hasta pos/rot en el mundo
        IEnumerator Glide(Transform tr, Vector3 pos, Quaternion rot)
        {
            tr.SetParent(null, true);
            Vector3 p0 = tr.position; Quaternion r0 = tr.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, enterDuration);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                tr.position = Vector3.Lerp(p0, pos, e);
                tr.rotation = Quaternion.Slerp(r0, rot, e);
                yield return null;
            }
        }

        // altura del piso bajo 'p', ignorando el collider del auto (para no spawnear arriba)
        float GroundYIgnoring(CarController c, Vector3 p)
        {
            Vector3 start = p + Vector3.up * 4f;
            var hits = Physics.RaycastAll(start, Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = c.transform.position.y; float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                var t = h.collider.transform;
                if (t == c.transform || t.IsChildOf(c.transform)) continue; // ignorar el auto
                float d = start.y - h.point.y;
                if (d >= 0f && d < bestDist) { bestDist = d; bestY = h.point.y; }
            }
            return bestY;
        }

        IEnumerator AnimateDoor(CarController c, Transform door, bool open, float dur)
        {
            if (door == null) yield break;
            if (!doorClosed.ContainsKey(door)) doorClosed[door] = door.localRotation;
            Quaternion closed = doorClosed[door];
            float sign = c.transform.InverseTransformPoint(door.position).x < 0f ? 1f : -1f;
            Quaternion openRot = closed * Quaternion.Euler(0f, sign * doorOpenDeg, 0f);
            Quaternion from = door.localRotation, to = open ? openRot : closed;
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
            var target = currentTarget;
            string msg = null;
            if (car != null)
            {
                if (target != null && !target.isSeat && target.part == myDoor)
                    msg = openDoors.Contains(myDoor) ? "[ E ] Cerrar puerta" : "[ E ] Abrir puerta";
                else msg = "[ E ] Bajar";
            }
            else if (target != null)
            {
                if (target.isSeat) msg = "[ E ] Subir";
                else msg = openDoors.Contains(target.part) ? "[ E ] Cerrar puerta" : "[ E ] Abrir puerta";
            }
            if (msg == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width / 2 - 140, Screen.height - 90, 280, 32), GUIContent.none);
            GUI.Label(new Rect(Screen.width / 2 - 140, Screen.height - 90, 280, 32), msg, style);
        }
    }
}
