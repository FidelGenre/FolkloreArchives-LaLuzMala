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

                // drive-in corto (para ver que "estaciona"), después ASIENTO EXACTO en el punto:
                // el autopilot se traba en los árboles y no clava, así que al terminar teletransporto
                // el auto al punto/yaw que dio el owner (siempre queda ahí).
                Vector3 parkW = new Vector3(campParkXZ.x, campParkY, campParkXZ.y);
                float t = 0f;
                while (!autoDrive.HasArrived && t < 6f)
                {
                    if (t > 1f && Flat2(car.transform.position, parkW) <= 4f) break;
                    t += Time.deltaTime;
                    yield return null;
                }
                car.autoPilot = false;
                autoDrive.active = false;
                car.externalThrottle = 0f; car.externalSteer = 0f;

                // asentar EXACTO en el punto (posición + yaw), frenado del todo.
                var rb = car.GetComponent<Rigidbody>();
                Quaternion parkRot = Quaternion.Euler(0f, campParkYaw, 0f);
                car.transform.position = parkW;
                car.transform.rotation = parkRot;
                if (rb != null)
                {
                    rb.position = parkW; rb.rotation = parkRot;
                    if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                }
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

            // 1) BAJAR. Jugador con su rutina real (limpia asiento, pose sentada y tamaño). Después
            //    un clamp de Y: ExitRoutine a veces deja una Y absurda y el jugador "volaba" a 3500m.
            car.driving = false;
            var pvi = player.GetComponent<PlayerVehicleInteractor>();
            if (pvi != null && pvi.CurrentSeat != null) yield return pvi.ExitRoutine();
            ReassertCinematic();   // ExitRoutine reactiva control/cámara de persona -> la cenital manda
            { Vector3 pp = player.position; pp.y = car.transform.position.y; player.position = pp; }
            var pAnim = player.GetComponent<HumanWalkAnim>(); if (pAnim != null) pAnim.seated = false;

            // todos parados cerca del auto, mirando al auto.
            Vector3 cp = car.transform.position;
            Vector3 right = car.transform.right, back = -car.transform.forward;
            PlaceStanding(player, cp + right * -2.2f + back * 0.5f, cp);
            UnseatAndPlace(casual, cp + right * -2.6f + back * -1.2f, cp);
            UnseatAndPlace(green,  cp + right * -3.6f + back * 0.2f, cp);
            UnseatAndPlace(chica,  cp + right * -2.6f + back * 2.0f, cp);

            // 2) TODOS van a la CAJUELA (atrás del auto) a "sacar" las carpas (cámara de enfoque).
            Vector3 trunk = cp + back * 4.4f; trunk.y = GroundY(trunk, cp.y);
            var cc = player.GetComponent<CharacterController>();
            bool dc = false, dg = false, dh = false;
            StartCoroutine(WalkNpcTo(casual, trunk + right * -1.0f, () => dc = true));
            StartCoroutine(WalkNpcTo(green,  trunk + right *  1.0f, () => dg = true));
            StartCoroutine(WalkNpcTo(chica,  trunk + back  *  1.0f, () => dh = true));
            yield return WalkPlayerTo(player, cc, trunk + back * 1.4f);   // el jugador también (CC apagado)
            float tw = 0f; while (!(dc && dg && dh) && tw < 8f) { tw += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(1.2f);   // sacan las tiendas de la cajuela

            // 3) SE QUITA la cámara de enfoque -> control al jugador (1ª persona). Desde acá ve
            //    de cerca cómo arman las carpas (no desde el ángulo que enfoca el auto).
            if (cc != null) cc.enabled = true;
            RestoreControl();

            // 4) los NPCs llevan/arman sus carpas EN SU LUGAR. La chica comparte con MaleCasual.
            //    Tu carpa = _playerTent (la "morada"): queda oculta, la armás con E (próximo paso).
            StartCoroutine(WalkThenTent(casual, casualChicaWalk + new Vector3(0.9f, 0f, 0f),   casualChicaYaw, true,  null));
            StartCoroutine(WalkThenTent(green,  greenWalk,                                     greenYaw,       true,  null));
            StartCoroutine(WalkThenTent(chica,  casualChicaWalk + new Vector3(-0.9f, 0f, 0.4f), casualChicaYaw, false, null));
        }

        // camina un NPC hasta 'dest' (sin armar carpa) y avisa 'done'.
        IEnumerator WalkNpcTo(Transform npc, Vector3 dest, System.Action done)
        {
            if (npc == null) { done?.Invoke(); yield break; }
            int guard = 0;
            while (Flat2(npc.position, dest) > 0.5f && guard++ < 3000)
            {
                StepToward(npc, dest, 2.2f);
                yield return null;
            }
            done?.Invoke();
        }

        // camina al JUGADOR scripteado hasta 'dest' (CC apagado). NO re-habilita el CC: lo hace el
        // caller cuando toca devolver el control.
        IEnumerator WalkPlayerTo(Transform player, CharacterController cc, Vector3 dest)
        {
            if (cc != null) cc.enabled = false;
            int guard = 0;
            while (Flat2(player.position, dest) > 0.6f && guard++ < 3000)
            {
                StepToward(player, dest, 2.3f);
                yield return null;
            }
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

        // camina un NPC hasta 'dest', lo deja mirando 'finalYaw', y si 'raiseTent' arma (pop) la
        // carpa oculta MÁS CERCANA, EN SU LUGAR (no la mueve: el campamento ya está bien dispuesto).
        IEnumerator WalkThenTent(Transform npc, Vector3 dest, float finalYaw, bool raiseTent, System.Action done)
        {
            if (npc == null) { done?.Invoke(); yield break; }
            int guard = 0;
            while (Flat2(npc.position, dest) > 0.5f && guard++ < 3000)
            {
                StepToward(npc, dest, 2.2f);
                yield return null;
            }
            FaceYaw(npc, finalYaw);
            if (raiseTent)
            {
                var tent = NearestHiddenTent(npc.position);
                if (tent != null)
                {
                    Vector3 full = tent.transform.localScale == Vector3.zero ? Vector3.one : tent.transform.localScale;
                    tent.transform.localScale = full * 0.05f;   // achico ANTES de activar (sin flash)
                    tent.SetActive(true);
                    yield return PopScale(tent, full);
                }
            }
            done?.Invoke();
        }

        // carpa OCULTA (aún sin armar) más cercana a 'pos'.
        GameObject NearestHiddenTent(Vector3 pos)
        {
            GameObject best = null; float bd = float.MaxValue;
            foreach (var go in _tents)
            {
                if (go == null || go.activeSelf || go == _playerTent) continue;   // ya armada o es la del jugador
                float d = Flat2(go.transform.position, pos);
                if (d < bd) { bd = d; best = go; }
            }
            return best;
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
            np.y = GroundY(np, pos.y);
            tr.position = np;
            tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }

        // altura del piso bajo 'p' (raycast). Fallback: 'fallbackY'.
        static float GroundY(Vector3 p, float fallbackY)
        {
            // cast local (barato); si falla porque p.y viene absurdo (ej. tras ExitRoutine), cast
            // desde bien arriba para encontrar el piso igual y no dejar al personaje flotando.
            if (Physics.Raycast(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, out var hit, 12f))
                return hit.point.y;
            if (Physics.Raycast(new Vector3(p.x, 400f, p.z), Vector3.down, out var hit2, 2000f))
                return hit2.point.y;
            return fallbackY;
        }

        // para un personaje PARADO en 'pos' mirando 'faceTarget' (maneja el CharacterController).
        static void PlaceStanding(Transform t, Vector3 pos, Vector3 faceTarget)
        {
            if (t == null) return;
            var cc = t.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            pos.y = GroundY(pos, pos.y);
            t.position = pos;
            Vector3 look = faceTarget - pos; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(look.normalized);
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
