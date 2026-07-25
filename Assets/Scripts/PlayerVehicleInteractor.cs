// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PlayerVehicleInteractor.cs — subir/bajar del auto, MANUAL, con E.
//    Afuera, cerca de una PUERTA:  E abre / cierra esa puerta.
//    Afuera, PEGADO a un asiento (puerta abierta):  E te sienta.
//    Sentado con la puerta CERRADA:  E abre la puerta nomás (seguís sentado).
//    Sentado con la puerta ABIERTA, mirándola:  E la cierra (seguís sentado).
//    Sentado con la puerta ABIERTA, mirando para otro lado:  E te baja.
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
        bool flashlightWasOn;   // estado de la linterna al subir (para restaurarlo al bajar)
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
                    if (!openDoors.Contains(myDoor))
                    {
                        // puerta cerrada → abrirla nomás, seguís sentado (owner: "cerre la
                        // puerta y luego al querer abrirla ya no me sale opcion")
                        StartCoroutine(SetDoor(car, myDoor, true));
                    }
                    else if (LookingAtDoor(myDoor))
                    {
                        // puerta abierta y mirándola → cerrarla, seguís sentado (owner:
                        // "apunto a la puerta y no me deja cerrarla" -- el raycast fallaba
                        // sentado tan cerca, seguramente pegándole antes al propio collider
                        // del asiento; ahora es un chequeo de ángulo puro, sin física)
                        StartCoroutine(SetDoor(car, myDoor, false));
                    }
                    else
                    {
                        // puerta abierta y NO mirándola → bajar
                        StartCoroutine(ExitRoutine());
                    }
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

        // Sentado, "¿estoy mirando la puerta?" -- ángulo puro, sin física. La MIRA
        // (RaycastTarget/SphereCast) sentado tan cerca fallaba: probablemente el propio
        // collider del asiento (donde está parada la cámara) se interponía primero.
        bool LookingAtDoor(Transform door)
        {
            if (door == null || cam == null) return false;
            Vector3 toDoor = door.position - cam.position;
            if (toDoor.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(cam.forward, toDoor) < 45f;
        }

        // MIRA invisible: qué parte del auto apunta el centro de la pantalla.
        // SphereCast (no un rayo de radio 0) porque sentado adentro, muy cerca de la
        // puerta, un rayo finito fallaba fácil por unos grados de diferencia -- eso
        // fue justo lo que causaba "quiero cerrar la puerta y me bajo" antes.
        CarInteractable RaycastTarget()
        {
            if (cam == null) return null;
            var hits = Physics.SphereCastAll(cam.position, 0.15f, cam.forward, 4.5f, ~0, QueryTriggerInteraction.Collide);
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
            // owner: "apuntando a la puerta... no me dice cerrar me dice bajar" -- si
            // la puerta que el jugador realmente ABRIÓ para subir no es la geométricamente
            // más cercana al asiento (pasa fácil con auto/asientos reescalados), myDoor
            // terminaba apuntando a OTRA puerta (cerrada), y por eso E te bajaba en vez
            // de cerrar la que sí estaba abierta. Preferí una puerta ABIERTA cercana
            // antes que la más cercana a secas.
            Transform best = null; float bd = float.MaxValue;
            Transform bestOpen = null; float bdOpen = float.MaxValue;
            foreach (var d in c.doors)
            {
                if (d == null) continue;
                float dd = Vector3.Distance(d.position, to);
                if (dd < bd) { bd = dd; best = d; }
                if (openDoors.Contains(d) && dd < bdOpen) { bdOpen = dd; bestOpen = d; }
            }
            return bestOpen != null ? bestOpen : best;
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

            // owner: "al entrar deberia apagarse mi linterna y usarse las del auto con
            // la misma tecla que la normal" -- solo al MANEJAR (los faros son del
            // auto, no tiene sentido para un pasajero).
            if (c.driving && explorer != null)
            {
                flashlightWasOn = explorer.FlashlightOn;
                explorer.SetFlashlight(false);
            }
            busy = false;
        }

        IEnumerator ExitRoutine()
        {
            busy = true;
            var c = car; var seat = mySeat; var door = myDoor;
            bool wasDriving = c.driving;
            car = null; mySeat = null; myDoor = null; c.driving = false;

            // apagar los faros del auto y devolverle al jugador su linterna como
            // estaba antes de subir (prendida o apagada).
            if (wasDriving)
            {
                c.SetHeadlights(false);
                if (explorer != null) explorer.SetFlashlight(flashlightWasOn);
            }

            if (door != null && !openDoors.Contains(door)) { yield return AnimateDoor(c, door, true, 0.30f); openDoors.Add(door); }

            // bajar JUSTO al lado de la PUERTA (no del asiento) que usaste, sobre el piso.
            // owner: "al bajar no baja bien sale la camara para otro lado" -- el 1.5f fijo
            // se calibró para un auto mucho más chico; con el Retro Car (6.6m) el jugador
            // terminaba re-posicionado ADENTRO/pisando el collider del auto, y Unity lo
            // empujaba para cualquier lado al resolver el solapamiento. Fix: la distancia
            // se calcula del ancho REAL del BoxCollider del auto, usando la posición de la
            // PUERTA (no del asiento, que tiene offsets de cámara/ojo no puramente
            // laterales) para decidir el lado.
            Vector3 doorRef = door != null ? door.position : seat.position;
            Vector3 sideDir = (doorRef - c.transform.position); sideDir.y = 0f;
            if (sideDir.sqrMagnitude < 0.01f) sideDir = -c.transform.right;
            sideDir.Normalize();
            var carCol = c.GetComponent<BoxCollider>();
            float clearance = (carCol != null ? carCol.size.x * 0.5f : 1.0f) + 0.8f;
            Vector3 side = c.transform.position + sideDir * clearance;
            side.y = GroundYIgnoring(c, side) + 0.05f;
            transform.position = side;
            // owner: "mete un 180 la camara... no se queda apuntando como si estuviera
            // saliendo por la puerta para adelante" -- quedar mirando "hacia afuera"
            // (perpendicular al auto) obligaba a GIRAR la cámara durante la transición
            // desde cómo mirabas sentado. Ahora encara la misma dirección que el auto
            // (su frente/dirección de viaje), sin girar nada en el proceso.
            transform.rotation = c.transform.rotation;
            lookYaw = 0f; lookPitch = 0f;

            // deslizar la cámara del asiento hasta el ojo del jugador (SUAVE, camino corto).
            // Solo la POSICIÓN se anima -- la rotación se mantiene fija en la que ya tenía
            // sentado (sin girar durante la transición) y se ajusta de un salto (sin
            // animar) recién al reparentarla, así no hay ningún giro visible de por medio.
            Vector3 targetPos = camParent.TransformPoint(camLocalPos);
            yield return Glide(cam, targetPos, cam.rotation);
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
            // Bisagra en el eje vertical del MUNDO, no el eje Y "local" del nodo tal
            // como vino del FBX (el pack nuevo trae los pivots de puerta con una
            // rotación propia -- rotar en su Y local abría la puerta en diagonal).
            Vector3 hingeAxis = door.parent.InverseTransformDirection(Vector3.up);
            Quaternion openRot = Quaternion.AngleAxis(sign * doorOpenDeg, hingeAxis) * closed;
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
                // mismo criterio que la acción real de E: estado de la puerta + ángulo de mira.
                if (!openDoors.Contains(myDoor)) msg = "[ E ] Abrir puerta";
                else if (LookingAtDoor(myDoor)) msg = "[ E ] Cerrar puerta";
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
