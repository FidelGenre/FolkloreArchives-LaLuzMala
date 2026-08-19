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
        public Vector3 overheadCamPos  = new Vector3(222.8298f, 25.25194f, 216.8119f); // posición de la cámara
        public Vector3 overheadCamLook = new Vector3(234.1218f, 24.6f, 213.4899f);     // mira al auto/donde bajan

        [Header("Bajada (puntos del owner)")]
        public Vector3 playerWalk      = new Vector3(250.1038f, 23.00087f, 224.2696f); // vos + Rufus (tu carpa la armás con E)
        public float   playerYaw       = -27.062f;
        public Vector3 casualChicaWalk = new Vector3(240.6869f, 22.68489f, 237.0636f); // MaleCasual + la chica
        public float   casualChicaYaw  = 128.698f;
        public Vector3 greenWalk       = new Vector3(250.0287f, 23.05577f, 237.5826f); // MaleGreenJkt ("el negro")
        public float   greenYaw        = -155.462f;

        [Header("Carpas (se mueven a cada punto y aparecen con pop al llegar)")]
        public string playerTentName = "Tents_Orange";   // la tuya (la armás con E, más adelante)
        public string casualTentName = "Tents_Green";    // MaleCasual + la chica
        public string greenTentName  = "Tents_DarkBlue"; // MaleGreenJkt

        Camera _overhead;   // cámara cenital (se mantiene durante toda la bajada/armado)
        Transform _campsite;
        readonly Dictionary<string, GameObject> _tents = new Dictionary<string, GameObject>();

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

            // bajar: el jugador deja de manejar; todos parados cerca del auto.
            car.driving = false;
            Vector3 cp = car.transform.position;
            Vector3 right = car.transform.right, back = -car.transform.forward;
            PlaceStanding(player, cp + right * -2.2f + back * 0.5f, playerWalk);
            UnseatAndPlace(casual, cp + right * -2.6f + back * -1.2f, casualChicaWalk);
            UnseatAndPlace(green,  cp + right * -3.6f + back * 0.2f, greenWalk);
            UnseatAndPlace(chica,  cp + right * -2.6f + back * 2.0f, casualChicaWalk);

            // NPCs caminan a su punto; al llegar arman (pop) su carpa. La chica comparte carpa
            // con MaleCasual (no arma otra), va al mismo punto con un offset para no encimarse.
            bool aCasual = false, aGreen = false, aChica = false;
            StartCoroutine(WalkThenTent(casual, casualChicaWalk + new Vector3(0.9f, 0f, 0f), casualChicaYaw, casualTentName, () => aCasual = true));
            StartCoroutine(WalkThenTent(green,  greenWalk,                                   greenYaw,       greenTentName,  () => aGreen  = true));
            StartCoroutine(WalkThenTent(chica,  casualChicaWalk + new Vector3(-0.9f, 0f, 0.4f), casualChicaYaw, null,        () => aChica  = true));

            // el jugador camina SCRIPTEADO hasta su punto (cámara cenital).
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;   // mover el transform a mano
            int guard = 0;
            while (Flat2(player.position, playerWalk) > 0.6f && guard++ < 3000)
            {
                StepToward(player, playerWalk, 2.3f);
                yield return null;
            }
            FaceYaw(player, playerYaw);
            if (cc != null) cc.enabled = true;

            // tu carpa: la dejo posicionada en tu punto pero OCULTA (la armás con E, próximo paso).
            MoveTent(playerTentName, playerWalk, playerYaw, false);

            // esperar a que los NPCs terminen de llegar/armar (tope 8s por las dudas).
            float tw = 0f;
            while (!(aCasual && aGreen && aChica) && tw < 8f) { tw += Time.deltaTime; yield return null; }

            RestoreControl();   // cámara a 1ª persona + control (armás tu carpa + juntás leña)
        }

        // saca un NPC del auto (lo desparenta, lo para cerca del auto, lo pone de pie -> camina).
        void UnseatAndPlace(Transform npc, Vector3 pos, Vector3 faceTarget)
        {
            if (npc == null) return;
            npc.SetParent(null, true);
            var fw = npc.GetComponent<FriendWander>(); if (fw != null) fw.enabled = false;
            var anim = npc.GetComponent<HumanWalkAnim>(); if (anim != null) anim.seated = false;
            PlaceStanding(npc, pos, faceTarget);
        }

        // camina un NPC hasta 'dest', lo deja mirando 'finalYaw', y si tiene carpa la arma (pop).
        IEnumerator WalkThenTent(Transform npc, Vector3 dest, float finalYaw, string tentName, System.Action done)
        {
            if (npc == null) { done?.Invoke(); yield break; }
            int guard = 0;
            while (Flat2(npc.position, dest) > 0.5f && guard++ < 3000)
            {
                StepToward(npc, dest, 2.2f);
                yield return null;
            }
            FaceYaw(npc, finalYaw);
            if (!string.IsNullOrEmpty(tentName) && _tents.TryGetValue(tentName, out var tg) && tg != null)
            {
                // arma la carpa en el punto donde quedó, con su misma orientación: aparece con pop.
                Vector3 full = tg.transform.localScale == Vector3.zero ? Vector3.one : tg.transform.localScale;
                tg.transform.localScale = full * 0.05f;   // achico ANTES de activar (sin flash)
                MoveTent(tentName, npc.position, finalYaw, true);
                yield return PopScale(tg, full);
            }
            done?.Invoke();
        }

        // reposiciona una carpa (por nombre) en 'pos' pegada al piso, mirando 'yaw', y la activa/oculta.
        void MoveTent(string name, Vector3 pos, float yaw, bool show)
        {
            if (string.IsNullOrEmpty(name) || !_tents.TryGetValue(name, out var go) || go == null) return;
            pos.y = GroundY(pos, pos.y);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.SetActive(show);
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
                _tents[child.name] = child.gameObject;
                child.gameObject.SetActive(false);
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
            if (Physics.Raycast(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, out var hit, 12f))
                return hit.point.y;
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
            go.transform.position = overheadCamPos != Vector3.zero ? overheadCamPos
                                  : new Vector3(overheadCamLook.x, overheadCamLook.y + 28f, overheadCamLook.z - 14f);
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
