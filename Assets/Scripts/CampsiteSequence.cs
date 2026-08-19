// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CampsiteSequence.cs — director del CAPÍTULO CAMPAMENTO (arranca al llegar
//  manejando desde la YPF). owner: manejás hasta un punto, de ahí el auto se
//  estaciona SOLO, una cámara cenital (3ª persona desde arriba) muestra el
//  campamento, y bajan todos scripteados. Después: se arman las carpas/fogata,
//  se hace de noche, comen/hablan, duermen, y Rufus se levanta y ve la Luz Mala.
//  Se construye por etapas (como YpfStorySequence). Lo dispara YpfStorySequence
//  al terminar de subir al auto (Etapa 7).
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FolkloreArchives
{
    public class CampsiteSequence : MonoBehaviour
    {
        OpeningDriveSequence op;
        CarController car;
        CarAutoDrive autoDrive;

        [Header("Llegada (coords del owner)")]
        public Vector3 driveTriggerPos = new Vector3(242.22f, 24.71229f, 199.8321f); // manejás hasta acá -> el auto sigue SOLO
        public float   driveTriggerRadius = 10f;
        public Vector2 campParkXZ = new Vector2(234.1218f, 213.4899f); // dónde FRENA/estaciona el auto (XZ, owner)
        public float   campParkY   = 24.0379f;            // altura del punto de estacionado
        public float   campParkYaw = -40.427f;           // yaw final del auto al estacionar

        [Header("Cámara 3ª persona")]
        // owner: puso el TEST_PLAYER donde/como quiere la cámara. Pos = pies (24.52549) + 2.30 de
        // altura de ojos (offset de la cámara del jugador). Rot = la del test player (horizontal).
        public Vector3 overheadCamPos  = new Vector3(219.4251f, 26.82549f, 215.1563f); // pos del test player (altura ojos)
        public Vector3 overheadCamLook = new Vector3(234.12f, 24.5f, 213.49f);         // MIRA al auto estacionado (no al cielo)

        [Header("Bajada (puntos del owner)")]
        public Vector3 playerWalk      = new Vector3(250.309f, 23.01033f, 224.0965f); // vos + Rufus (tu carpa la armás con E)
        public float   playerYaw       = -24.107f;
        public Vector3 casualChicaWalk = new Vector3(240.6869f, 22.68489f, 237.0636f); // MaleCasual + la chica
        public float   casualChicaYaw  = 128.698f;
        public Vector3 greenWalk       = new Vector3(250.0287f, 23.05577f, 237.5826f); // MaleGreenJkt ("el negro")
        public float   greenYaw        = -155.462f;

        [Header("Bajada: puntos exactos jugador/perro (owner)")]
        public Vector3 playerExitPos = new Vector3(237.3621f, 24.10073f, 209.8857f); // el jugador baja acá
        public float   playerExitYaw = -45.977f;
        public Vector3 dogExitPos    = new Vector3(232.31f, 24.03395f, 212.8251f);   // Rufus baja del lado del acompañante
        public float   dogExitYaw    = 137.143f;

        [Header("Armado: carpa chica+chico, tronco, leña (owner)")]
        public Vector3 tentPairPos = new Vector3(240.5201f, 22.88509f, 232.7946f); // chica + chico ponen la carpa
        public float   tentPairYaw = 9.125f;
        public Vector3 chicaSitPos = new Vector3(246.0744f, 23.76039f, 229.2029f); // la chica se sienta en este tronco
        public float   chicaSitYaw = -6.075f;
        public Vector3 woodPos     = new Vector3(240.1889f, 25.14602f, 247.7799f); // el chico busca leña acá
        public float   woodYaw     = -29.675f;
        public Vector3 greenTentPos = new Vector3(249.8865f, 23.07505f, 234.3149f); // el negro pone su carpa acá
        public float   greenTentYaw = -120.779f;
        public Vector3 greenSitPos  = new Vector3(248.6f, 23.7f, 231.8f);           // el negro se sienta (tronco este; dame el exacto si es otro)

        [Header("Carpa del jugador (la arma con E; los NPCs arman las otras dos)")]
        public string playerTentName = "Tents_DarkBlue"; // la "morada" (owner) -- derecha, cerca del auto

        Camera _overhead;   // cámara cenital (se mantiene durante toda la bajada/armado)
        Transform _campsite;
        readonly List<GameObject> _tents = new List<GameObject>(); // carpas ocultas al inicio; se revelan EN SU LUGAR
        GameObject _playerTent;   // la carpa del jugador (la arma con E, próximo paso); NO la revelan los NPCs

        public void Begin(OpeningDriveSequence seq)
        {
            op = seq;
            car = Object.FindFirstObjectByType<CarController>();
            autoDrive = car != null ? car.GetComponent<CarAutoDrive>() : null;
            HideCampForSetup();   // las carpas arrancan OCULTAS -> aparecen off-camera al llegar
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (car == null) { Debug.LogWarning("[Camp] no encontré el auto"); yield break; }

            // 1) manejás vos hasta el TRIGGER. Si TODAVÍA no está seteado (falta la coord del owner),
            // el capítulo NO arranca -> el checkpoint 2 queda en la YPF como antes, y manejás normal.
            if (driveTriggerPos == Vector3.zero)
            {
                Debug.LogWarning("[Camp] falta driveTriggerPos -> el capítulo campamento no arranca (seteá el trigger).");
                yield break;
            }
            Debug.Log($"<color=cyan>[Camp] manejá hasta el campamento (trigger en {driveTriggerPos}, r={driveTriggerRadius})</color>");
            yield return new WaitUntil(() => Flat2(car.transform.position, driveTriggerPos) <= driveTriggerRadius);

            // 2) el auto se ESTACIONA SOLO: autopilot hasta el punto del campamento.
            Debug.Log("<color=cyan>[Camp] el auto se estaciona solo...</color>");
            PartyController.CinematicLock = true;   // no manejás ni movés cámara durante la llegada
            if (autoDrive != null)
            {
                autoDrive.waypoints = new[] { campParkXZ };
                autoDrive.hasFinalYaw = true;
                autoDrive.finalYaw = campParkYaw;
                car.autoPilot = true;
                autoDrive.active = true;

                // 3) cámara CENITAL mientras estaciona
                MakeOverheadCam();

                // owner: "que vaya manejando hasta ahí SIN teletransportarse". El autopilot frena a
                // arriveRadius (8m) del punto -> quedaba "un punto más atrás". Lo dejo acercarse y
                // remato con un GLIDE SUAVE (~1.5s) que recorre el último tramo hasta el punto/yaw
                // EXACTO del owner. No es un salto: el auto se desliza gradualmente y clava ahí.
                float t = 0f;
                while (!autoDrive.HasArrived && t < 25f) { t += Time.deltaTime; yield return null; }
                car.autoPilot = false;
                autoDrive.active = false;
                car.externalThrottle = 0f; car.externalSteer = 0f;
                var rb = car.GetComponent<Rigidbody>();

                Vector3 parkW = new Vector3(campParkXZ.x, campParkY, campParkXZ.y);
                Quaternion parkRot = Quaternion.Euler(0f, campParkYaw, 0f);
                Vector3 p0 = car.transform.position; Quaternion r0 = car.transform.rotation;
                float g = 0f;
                while (g < 1f)
                {
                    g += Time.deltaTime / 1.5f;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(g));
                    Vector3 pos = Vector3.Lerp(p0, parkW, k);
                    Quaternion rot = Quaternion.Slerp(r0, parkRot, k);
                    car.transform.position = pos; car.transform.rotation = rot;
                    if (rb != null && !rb.isKinematic) { rb.position = pos; rb.rotation = rot; }
                    yield return null;
                }
                car.transform.position = parkW; car.transform.rotation = parkRot;
                if (rb != null && !rb.isKinematic) { rb.position = parkW; rb.rotation = parkRot; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }
            else
            {
                MakeOverheadCam();
            }

            Debug.Log("<color=cyan>[Camp] llegaron al campamento -> bajan y caminan</color>");
            yield return DisembarkAndWalk();
            // (próximo: armar carpas/fogata -> noche -> comer -> dormir -> Rufus)
        }

        // Bajan TODOS del auto y cada grupo camina a SU punto (cámara cenital todo el tiempo).
        // Al llegar cada NPC, su carpa aparece con un pop. Cuando llegan todos, te devuelvo el
        // control en 1ª persona en tu punto (después: armar tu carpa + juntar leña).
        IEnumerator DisembarkAndWalk()
        {
            Transform player = (op != null && op.player != null) ? op.player.transform : null;
            Transform casual = op != null ? op.friendMaleCasual  : null;
            Transform green  = op != null ? op.friendMaleGreenJkt : null;
            Transform chica  = op != null ? op.friendFemaleSec    : null;

            if (player == null)
            {
                Debug.LogWarning("[Camp] no hay player -> devuelvo control.");
                RestoreControl();
                yield break;
            }

            // 1) BAJAR. Jugador y perro con su rutina real (limpia asiento/pose/tamaño) + clamp de Y
            //    (ExitRoutine a veces dejaba una Y absurda y "volaban"). Cada uno baja en SU punto.
            car.driving = false;
            OpenTrunk(true);   // cajuela ABIERTA para sacar las carpas (queda abierta)
            Vector3 cp = car.transform.position;
            Vector3 right = car.transform.right, back = -car.transform.forward;

            // jugador: baja en su punto (owner)
            var pvi = player.GetComponent<PlayerVehicleInteractor>();
            if (pvi != null && pvi.CurrentSeat != null) yield return pvi.ExitRoutine();
            { Vector3 pp = player.position; pp.y = cp.y; player.position = pp; }
            var pAnim = player.GetComponent<HumanWalkAnim>(); if (pAnim != null) pAnim.seated = false;
            PlaceStandingYaw(player, playerExitPos, playerExitYaw);

            // Rufus: baja del lado del acompañante (owner)
            Transform dog = op.dog != null ? op.dog.transform : null;
            if (op.dog != null && op.dog.CurrentSeat != null) yield return op.dog.ExitRoutine();
            if (dog != null) PlaceStandingYaw(dog, dogExitPos, dogExitYaw);

            ReassertCinematic();   // ExitRoutine reactiva control/cámaras -> la cenital manda

            // NPCs: bajan y se paran DETRÁS de la cajuela, MIRÁNDOLA (al auto). Se ABRE la cajuela.
            UnseatAndPlace(casual, cp + right * -2.6f + back * -1.2f, cp);
            UnseatAndPlace(green,  cp + right * -3.6f + back * 0.2f, cp);
            UnseatAndPlace(chica,  cp + right * -2.6f + back * 2.0f, cp);

            // 2) desde la puerta, el JUGADOR y los 3 NPCs CAMINAN hasta la cajuela y quedan
            //    MIRÁNDOLA (facing al auto). Sacan las carpas.
            Vector3 trunkBack = cp + back * 5.2f; trunkBack.y = GroundY(trunkBack, cp.y);
            var cc = player.GetComponent<CharacterController>();
            bool dc = false, dg = false, dh = false;
            StartCoroutine(WalkNpcTo(casual, trunkBack + right * -1.7f, cp, () => dc = true));
            StartCoroutine(WalkNpcTo(green,  trunkBack + right *  1.7f, cp, () => dg = true));
            StartCoroutine(WalkNpcTo(chica,  trunkBack + right *  0.0f, cp, () => dh = true));
            yield return WalkPlayerTo(player, cc, trunkBack + right * 2.7f, cp);   // el jugador (CC apagado)
            float tw = 0f; while (!(dc && dg && dh) && tw < 8f) { tw += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(1.4f);   // sacan las tiendas de la cajuela

            // 3) SE QUITA la cámara de enfoque -> control al jugador (1ª persona). La cajuela queda
            //    ABIERTA (owner). Desde acá ve de cerca cómo arman las carpas.
            if (cc != null) cc.enabled = true;
            RestoreControl();

            // 4) ARMADO. El negro arma SU carpa al lado (como antes). La chica y el chico (MaleCasual)
            //    ponen SU carpa en tentPairPos; después la chica se SIENTA en el tronco y el chico va
            //    a buscar LEÑA y la lleva a la fogata. Tu carpa (morada) la armás con E (próximo).
            Vector3 center = _campsite != null ? _campsite.position : new Vector3(246f, cp.y, 232f);
            var npcTents = new List<GameObject>();
            foreach (var t in _tents) if (t != null && t != _playerTent) npcTents.Add(t);
            GameObject tentPair  = npcTents.Count > 0 ? npcTents[0] : null; // carpa chica+chico
            GameObject tentGreen = npcTents.Count > 1 ? npcTents[1] : null; // carpa del negro
            StartCoroutine(GreenTentThenSit(green, tentGreen, center));
            StartCoroutine(PairTentThenTasks(casual, chica, tentPair, center));
        }

        // El negro camina a greenTentPos, arma AHÍ su carpa (pop), y después va al tronco y se
        // sienta mirando la fogata.
        IEnumerator GreenTentThenSit(Transform green, GameObject tent, Vector3 fire)
        {
            bool a = false;
            if (green != null) StartCoroutine(WalkNpcTo(green, greenTentPos, greenTentPos + Fwd(greenTentYaw), () => a = true)); else a = true;
            float t = 0f; while (!a && t < 12f) { t += Time.deltaTime; yield return null; }

            if (tent != null)
            {
                Vector3 tp = greenTentPos; tp.y = GroundY(greenTentPos, greenTentPos.y);
                tent.transform.position = tp;
                tent.transform.rotation = Quaternion.Euler(0f, greenTentYaw, 0f);
                Vector3 full = tent.transform.localScale == Vector3.zero ? Vector3.one : tent.transform.localScale;
                tent.transform.localScale = full * 0.05f;
                tent.SetActive(true);
                yield return PopScale(tent, full);
            }

            if (green != null)
            {
                bool b = false;
                StartCoroutine(WalkNpcTo(green, greenSitPos, fire, () => b = true));
                t = 0f; while (!b && t < 12f) { t += Time.deltaTime; yield return null; }
                Vector3 d = fire - greenSitPos; float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                PlaceSeated(green, greenSitPos, yaw);
            }
        }

        // La chica y el chico caminan a tentPairPos y arman AHÍ su carpa. Después: la chica va al
        // tronco y se sienta; el chico va a buscar leña y la lleva a la fogata.
        IEnumerator PairTentThenTasks(Transform chico, Transform chica, GameObject tent, Vector3 fire)
        {
            Vector3 fwd = Fwd(tentPairYaw), rgt = Right(tentPairYaw);
            Vector3 face = tentPairPos + fwd; // ambos miran hacia donde va la carpa
            bool a = false, b = false;
            if (chico != null) StartCoroutine(WalkNpcTo(chico, tentPairPos + rgt * -0.9f, face, () => a = true)); else a = true;
            if (chica != null) StartCoroutine(WalkNpcTo(chica, tentPairPos + rgt *  0.9f, face, () => b = true)); else b = true;
            float t = 0f; while (!(a && b) && t < 10f) { t += Time.deltaTime; yield return null; }

            // armar la carpa EN tentPairPos (mirando tentPairYaw), con pop.
            if (tent != null)
            {
                Vector3 tp = tentPairPos; tp.y = GroundY(tentPairPos, tentPairPos.y);
                tent.transform.position = tp;
                tent.transform.rotation = Quaternion.Euler(0f, tentPairYaw, 0f);
                Vector3 full = tent.transform.localScale == Vector3.zero ? Vector3.one : tent.transform.localScale;
                tent.transform.localScale = full * 0.05f;
                tent.SetActive(true);
                yield return PopScale(tent, full);
            }

            // la chica -> tronco (pos2) y se sienta; en paralelo el chico va por la leña.
            if (chica != null) StartCoroutine(SitOnLog(chica));
            if (chico != null) yield return FetchWood(chico, fire);
        }

        // camina la chica al tronco y la deja SENTADA (pose seated), mirando chicaSitYaw.
        IEnumerator SitOnLog(Transform chica)
        {
            bool a = false;
            StartCoroutine(WalkNpcTo(chica, chicaSitPos, chicaSitPos + Fwd(chicaSitYaw), () => a = true));
            float t = 0f; while (!a && t < 10f) { t += Time.deltaTime; yield return null; }
            PlaceSeated(chica, chicaSitPos, chicaSitYaw);
        }

        // el chico va a woodPos, "junta" leña (aparece un tronquito en sus manos), la lleva a la
        // fogata y la deja ahí.
        IEnumerator FetchWood(Transform chico, Vector3 fire)
        {
            bool a = false;
            StartCoroutine(WalkNpcTo(chico, woodPos, woodPos + Fwd(woodYaw), () => a = true));
            float t = 0f; while (!a && t < 14f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.9f);   // se agacha / junta

            var log = MakeCarriedLog();
            log.transform.SetParent(chico, false);
            log.transform.localPosition = new Vector3(0f, 1.15f, 0.4f);
            log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            bool b = false;
            StartCoroutine(WalkNpcTo(chico, fire + new Vector3(1.3f, 0f, 1.3f), fire, () => b = true));
            t = 0f; while (!b && t < 14f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.5f);

            // dejar la leña en la fogata.
            log.transform.SetParent(null, true);
            Vector3 fp = fire; fp.y = GroundY(fire, fire.y);
            log.transform.position = fp + new Vector3(0.3f, 0.1f, 0f);
            log.transform.rotation = Quaternion.Euler(0f, 20f, 90f);
        }

        // tronquito de "leña" (cilindro marrón sin collider) para llevar a la fogata.
        static GameObject MakeCarriedLog()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "LenaCargada";
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
            var r = go.GetComponent<Renderer>(); if (r != null) r.material.color = new Color(0.35f, 0.25f, 0.15f);
            return go;
        }

        // deja a un personaje SENTADO en 'pos' mirando 'yaw' (pose seated de HumanWalkAnim).
        static void PlaceSeated(Transform t, Vector3 pos, float yaw)
        {
            if (t == null) return;
            var cc = t.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            pos.y = GroundY(pos, pos.y, t);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = was;
            var anim = t.GetComponent<HumanWalkAnim>(); if (anim != null) anim.seated = true;
        }

        // direcciones planas desde un yaw (grados). Unity: forward=(sin,0,cos), right=(cos,0,-sin).
        static Vector3 Fwd(float yaw)   { float r = yaw * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)); }
        static Vector3 Right(float yaw) { float r = yaw * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r)); }

        // camina un NPC hasta 'dest' (sin armar carpa), lo deja mirando 'faceTarget', y avisa 'done'.
        IEnumerator WalkNpcTo(Transform npc, Vector3 dest, Vector3 faceTarget, System.Action done)
        {
            if (npc == null) { done?.Invoke(); yield break; }
            int guard = 0;
            while (Flat2(npc.position, dest) > 0.5f && guard++ < 3000)
            {
                StepToward(npc, dest, 2.2f);
                yield return null;
            }
            Vector3 look = faceTarget - npc.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) npc.rotation = Quaternion.LookRotation(look.normalized);
            done?.Invoke();
        }

        // camina al JUGADOR scripteado hasta 'dest' (CC apagado) y lo deja mirando 'faceTarget'. NO
        // re-habilita el CC: lo hace el caller cuando toca devolver el control.
        IEnumerator WalkPlayerTo(Transform player, CharacterController cc, Vector3 dest, Vector3 faceTarget)
        {
            if (cc != null) cc.enabled = false;
            int guard = 0;
            while (Flat2(player.position, dest) > 0.6f && guard++ < 3000)
            {
                StepToward(player, dest, 2.0f);
                yield return null;
            }
            Vector3 look = faceTarget - player.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) player.rotation = Quaternion.LookRotation(look.normalized);
        }

        // saca un NPC del auto (lo desparenta, lo para cerca del auto, lo pone de pie -> camina).
        // owner-fix: SetParent(null,FALSE)+scale 1 (como StandFriend). Con SetParent(null,true) el
        // amigo HEREDA la escala del auto y queda mal (chico/gigante) -> "los NPC no están".
        void UnseatAndPlace(Transform npc, Vector3 pos, Vector3 faceTarget)
        {
            if (npc == null) return;
            npc.SetParent(null, false);
            npc.localScale = Vector3.one;
            var fw = npc.GetComponent<FriendWander>(); if (fw != null) fw.enabled = false;
            var anim = npc.GetComponent<HumanWalkAnim>(); if (anim != null) anim.seated = false;
            PlaceStanding(npc, pos, faceTarget);
        }

        // camina un NPC hasta 'dest' (a 'speed'), lo deja mirando 'faceTarget', y si 'tent' no es
        // null la arma (pop) EN SU LUGAR. 'dest' ya viene AL LADO de la carpa (no encima).
        IEnumerator WalkThenRaise(Transform npc, Vector3 dest, GameObject tent, Vector3 faceTarget, float speed, System.Action done)
        {
            if (npc == null) { done?.Invoke(); yield break; }
            int guard = 0;
            while (Flat2(npc.position, dest) > 0.4f && guard++ < 4000)
            {
                StepToward(npc, dest, speed);
                yield return null;
            }
            Vector3 look = faceTarget - npc.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) npc.rotation = Quaternion.LookRotation(look.normalized);
            if (tent != null)
            {
                Vector3 full = tent.transform.localScale == Vector3.zero ? Vector3.one : tent.transform.localScale;
                tent.transform.localScale = full * 0.05f;   // achico ANTES de activar (sin flash)
                tent.SetActive(true);
                yield return PopScale(tent, full);
            }
            done?.Invoke();
        }

        // punto PARADO al lado de la carpa, del lado de AFUERA (alejándose del centro del campamento),
        // así el NPC la arma parado al costado y no encima.
        Vector3 BesideTent(GameObject tent, Vector3 center, float dist)
        {
            if (tent == null) return center;
            Vector3 tp = tent.transform.position;
            Vector3 outward = tp - center; outward.y = 0f;
            outward = outward.sqrMagnitude < 0.01f ? Vector3.forward : outward.normalized;
            Vector3 p = tp + outward * dist;
            p.y = GroundY(p, tp.y);
            return p;
        }

        // abre/cierra la CAJUELA. owner: "el asset dice que se puede abrir la cajuela". El builder
        // solo mete en car.doors las partes con "door" en el nombre, así que la cajuela (otro nombre)
        // queda AFUERA -> la busco en TODA la jerarquía del auto. Logueo todas las partes con mesh
        // para poder identificar el nombre exacto si el auto-detect por nombre falla.
        void OpenTrunk(bool open)
        {
            if (car == null) return;
            var cd = car.GetComponent<FolkloreArchives.Net.CarDoors>();
            if (cd == null) return;

            if (open)
            {
                string all = "";
                foreach (var tr in car.GetComponentsInChildren<Transform>(true))
                    if (tr.GetComponent<MeshFilter>() != null) all += tr.name + "; ";
                Debug.Log($"<color=cyan>[Camp] partes con mesh del auto: {all}</color>");
            }

            // buscar una parte tipo baúl por nombre (excluyendo puertas/vidrios).
            Transform trunk = null;
            foreach (var tr in car.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name.ToLower();
                if (n.Contains("door") || n.Contains("wind") || n.Contains("glass")) continue;
                if (n.Contains("trunk") || n.Contains("boot") || n.Contains("hatch") ||
                    n.Contains("tailgate") || n.Contains("cajuela") || n.Contains("cargo") ||
                    n.Contains("lid") || n.Contains("liftgate"))
                { trunk = tr; break; }
            }
            if (trunk != null) cd.SetDoor(trunk, open);
            else if (open) Debug.LogWarning("[Camp] no encontré parte de cajuela por nombre -> mirá el log de partes de arriba y decime cuál es.");
        }

        // "pop": escala la carpa de casi 0 a su tamaño real ('full') en ~0.6s (armado rápido, estilo PSX).
        IEnumerator PopScale(GameObject go, Vector3 full)
        {
            if (go == null) yield break;
            if (full == Vector3.zero) full = Vector3.one;
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0.05f, 1f, t / 0.6f);
                go.transform.localScale = full * k;
                yield return null;
            }
            go.transform.localScale = full;
        }

        // gira un transform para que mire un yaw (sólo Y).
        static void FaceYaw(Transform t, float yaw)
        {
            if (t != null) t.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // busca el objeto "Campsite" y OCULTA sus carpas (aparecen después, al armar el campamento).
        void HideCampForSetup()
        {
            var root = GameObject.Find("Campsite");
            if (root == null) { Debug.LogWarning("[Camp] no encontré el objeto 'Campsite' para ocultar carpas."); return; }
            _campsite = root.transform;
            foreach (Transform child in _campsite)
            {
                if (!child.name.StartsWith("Tents")) continue;
                _tents.Add(child.gameObject);
                if (child.name == playerTentName) _playerTent = child.gameObject; // la tuya (no la revelan los NPCs)
                child.gameObject.SetActive(false);
            }
        }

        // vuelve a imponer la cinemática (por si ExitRoutine reactivó control/cámara de persona):
        // apaga las cámaras de persona/perro (la cenital manda) y re-bloquea el control.
        void ReassertCinematic()
        {
            PartyController.CinematicLock = true;
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null)
            {
                if (party.personCam != null) party.personCam.gameObject.SetActive(false);
                if (party.dogCam != null)    party.dogCam.gameObject.SetActive(false);
            }
        }

        // mueve 'tr' un paso hacia 'target' (plano), pegado al piso, mirando hacia donde va.
        void StepToward(Transform tr, Vector3 target, float speed)
        {
            Vector3 pos = tr.position;
            Vector3 to = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
            float d = to.magnitude;
            if (d < 0.02f) return;
            Vector3 dir = to / d;
            Vector3 np = pos + dir * Mathf.Min(speed * Time.deltaTime, d);
            np.y = GroundY(np, pos.y, tr);   // <-- pasar 'tr' para que el raycast NO se pegue a su propio collider
            tr.position = np;
            tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }

        // altura del piso bajo 'p' (raycast). Fallback: 'fallbackY'. Si se pasa 'self', se apagan
        // sus colliders durante el raycast para que NO se pegue a sí mismo (era la causa de que
        // los personajes "subieran" ~2.4m por paso hasta el cielo).
        static float GroundY(Vector3 p, float fallbackY, Transform self = null)
        {
            Collider[] cols = self != null ? self.GetComponentsInChildren<Collider>(true) : null;
            bool[] were = null;
            if (cols != null)
            {
                were = new bool[cols.Length];
                for (int i = 0; i < cols.Length; i++) { were[i] = cols[i] != null && cols[i].enabled; if (cols[i] != null) cols[i].enabled = false; }
            }
            float y = fallbackY;
            if (Physics.Raycast(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, out var hit, 40f))
                y = hit.point.y;
            else if (Physics.Raycast(new Vector3(p.x, 400f, p.z), Vector3.down, out var hit2, 2000f))
                y = hit2.point.y;
            if (cols != null)
                for (int i = 0; i < cols.Length; i++) if (cols[i] != null) cols[i].enabled = were[i];
            return y;
        }

        // para un personaje PARADO en 'pos' mirando 'faceTarget' (maneja el CharacterController).
        static void PlaceStanding(Transform t, Vector3 pos, Vector3 faceTarget)
        {
            if (t == null) return;
            var cc = t.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            pos.y = GroundY(pos, pos.y, t);   // ignora su propio collider
            t.position = pos;
            Vector3 look = faceTarget - pos; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(look.normalized);
            if (cc != null) cc.enabled = was;
        }

        // igual que PlaceStanding pero con un YAW fijo (no mira a un target) -- para bajar al jugador
        // y al perro en su orientación exacta.
        static void PlaceStandingYaw(Transform t, Vector3 pos, float yaw)
        {
            if (t == null) return;
            var cc = t.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            pos.y = GroundY(pos, pos.y, t);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = was;
        }

        // devuelve el control: re-activa la cámara de la persona, saca el bloqueo y borra la cenital.
        void RestoreControl()
        {
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null && party.personCam != null) party.personCam.gameObject.SetActive(true);
            if (_overhead != null) Destroy(_overhead.gameObject);
            _overhead = null;
            PartyController.CinematicLock = false;
        }

        // cámara cenital (3ª persona desde arriba del campamento). Apaga las cámaras de persona/perro
        // y activa esta. Se apaga al volver al control normal.
        void MakeOverheadCam()
        {
            if (_overhead != null) return;
            var go = new GameObject("CampOverheadCam");
            go.transform.position = overheadCamPos;
            go.transform.LookAt(overheadCamLook);
            _overhead = go.AddComponent<Camera>();
            _overhead.tag = "MainCamera";
            _overhead.farClipPlane = 500f;
            go.AddComponent<AudioListener>();
            // apagar las cámaras del jugador/perro
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null)
            {
                if (party.personCam != null) party.personCam.gameObject.SetActive(false);
                if (party.dogCam != null)    party.dogCam.gameObject.SetActive(false);
            }
        }

        static float Flat2(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }
}
