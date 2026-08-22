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
using UnityEngine.InputSystem;

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
        public Vector3 playerExitPos = new Vector3(232.5957f, 24.02715f, 212.6393f); // el jugador baja acá (puerta)
        public float   playerExitYaw = 132.213f;
        public Vector3 dogExitPos    = new Vector3(232.31f, 24.03395f, 212.8251f);   // Rufus baja del lado del acompañante
        public float   dogExitYaw    = 137.143f;

        [Header("Armado: carpa chica+chico, tronco, leña (owner)")]
        public Vector3 tentPairPos = new Vector3(249.8865f, 23.07505f, 234.3149f); // dónde queda la carpa de la pareja
        public float   tentPairYaw = -120.779f;
        public Vector3 pairStandPos = new Vector3(251.9019f, 23.17876f, 235.0608f); // la pareja se para ACÁ; al llegar aparece su carpa
        public float   pairStandYaw = -39.627f;
        public Vector3 chicaSitPos = new Vector3(248.6f, 23.7f, 231.8f);           // chica+casual en el tronco que era del negro (este)
        public float   chicaSitYaw = -85.6f;                                        // mirando a la fogata desde ahí
        public Vector3 greenTentPos = new Vector3(240.5201f, 22.88509f, 232.7946f); // el negro pone su carpa acá (swap: donde era la de ellos)
        public float   greenTentYaw = 9.125f;
        public Vector3 greenSitPos  = new Vector3(246.0744f, 23.76039f, 229.2029f); // el negro en el tronco que era de ellos (sur); yaw auto a la fogata
        public Vector3 greenStandPos = new Vector3(239.8789f, 22.83138f, 233.3926f); // el negro queda PARADO al lado de su carpa (esperando que le hables)
        public float   greenStandYaw = 159.173f;
        public float   seatYOffset  = -1.3f;   // ajuste de altura al sentarse en el tronco (m). Más negativo = más abajo.

        [Header("Cajuela")]
        public float trunkOpenDeg = 70f;    // se abre girando sobre eje HORIZONTAL (se levanta). + = arriba, - = abajo.

        [Header("Tu parte: carpa (morada) + leña")]
        public Vector3 woodPlayerPos = new Vector3(229.8513f, 24.00745f, 231.7843f); // TODOS buscan la leña acá (casual, negro y vos)
        public float   playerReach   = 3.2f;   // distancia para tus interacciones con E
        string _playerHint;  // cartel [E] tuyo (se dibuja con InteractHint)
        string _playerSay;   // línea de diálogo (abajo, tipo guion)

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

            // 1) BAJAR EN LA PUERTA. El jugador y Rufus NO van a la cajuela: bajan al lado del auto,
            //    la cámara pasa YA a 1ª persona, y el jugador MIRA cómo los NPCs abren la cajuela.
            car.driving = false;
            Vector3 cp = car.transform.position;
            Vector3 right = car.transform.right, back = -car.transform.forward;
            Vector3 trunkBack = cp + back * 5.2f; trunkBack.y = GroundY(trunkBack, cp.y);

            // jugador: baja en la puerta, MIRANDO hacia la cajuela (para ver a los NPCs).
            var pAnim = player.GetComponent<HumanWalkAnim>();
            if (pAnim != null) pAnim.seated = false;   // limpiar la pose sentada YA (antes de la transición de ExitRoutine)
            var pvi = player.GetComponent<PlayerVehicleInteractor>();
            if (pvi != null && pvi.CurrentSeat != null) yield return pvi.ExitRoutine();
            { Vector3 pp = player.position; pp.y = cp.y; player.position = pp; }
            if (pAnim != null) pAnim.seated = false;
            PlaceStandingYaw(player, playerExitPos, playerExitYaw);   // en la puerta, mirando hacia la cajuela

            // Rufus: se queda SENTADO en el auto hasta que le abrís la puerta del acompañante (E).
            Transform dog = op.dog != null ? op.dog.transform : null;
            if (op.dog != null && op.dog.CurrentSeat != null) StartCoroutine(DogWaitsForDoor());
            else if (dog != null) PlaceStandingYaw(dog, dogExitPos, dogExitYaw);

            // NPCs: bajan cerca del auto.
            UnseatAndPlace(casual, cp + right * -2.6f + back * -1.2f, cp);
            UnseatAndPlace(green,  cp + right * -3.6f + back * 0.2f, cp);
            UnseatAndPlace(chica,  cp + right * -2.6f + back * 2.0f, cp);

            // 2) CONTROL al jugador YA (1ª persona), ni bien baja -> mira desde su propia cámara.
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            RestoreControl();

            // 3) los 3 NPCs CAMINAN a la cajuela, la ABREN (con ruido) y "sacan" las carpas. El
            //    jugador lo ve desde 1ª persona.
            bool dc = false, dg = false, dh = false;
            StartCoroutine(WalkNpcTo(casual, trunkBack + right * -1.7f, cp, () => dc = true));
            StartCoroutine(WalkNpcTo(green,  trunkBack + right *  1.7f, cp, () => dg = true));
            StartCoroutine(WalkNpcTo(chica,  trunkBack + right *  0.0f, cp, () => dh = true));
            float tw = 0f; while (!(dc && dg && dh) && tw < 10f) { tw += Time.deltaTime; yield return null; }
            OpenTrunk(true);   // los NPCs abren la cajuela
            yield return new WaitForSeconds(1.4f);   // sacan las tiendas de la cajuela

            // 4) ARMADO. El negro arma SU carpa al lado (como antes). La chica y el chico (MaleCasual)
            //    ponen SU carpa en tentPairPos; después la chica se SIENTA en el tronco y el chico va
            //    a buscar LEÑA y la lleva a la fogata. Tu carpa (morada) la armás con E (próximo).
            Vector3 center = _campsite != null ? _campsite.position : new Vector3(246f, cp.y, 232f);
            var npcTents = new List<GameObject>();
            foreach (var t in _tents) if (t != null && t != _playerTent) npcTents.Add(t);
            // SWAP owner: la de la PAREJA es la VERDE (npcTents[1]), la del NEGRO es la ROJA (npcTents[0]).
            // Antes estaban al revés -> el negro disparaba la de la pareja y viceversa.
            GameObject tentPair  = npcTents.Count > 1 ? npcTents[1] : null; // carpa chica+chico (verde)
            GameObject tentNegro = npcTents.Count > 0 ? npcTents[0] : null; // carpa del negro (roja)
            StartCoroutine(GreenTentThenStand(green, tentNegro, center));
            StartCoroutine(PairTentThenTasks(casual, chica, tentPair, center));

            // TU PARTE: recogés tu carpa de la cajuela, la ponés (fantasma celeste te marca dónde),
            // y después el chico te pide leña -> vas a buscarla.
            StartCoroutine(PlayerCampTasks());
        }

        // Rufus queda SENTADO en el auto hasta que el jugador le ABRE la puerta del acompañante;
        // ahí baja y te sigue.
        IEnumerator DogWaitsForDoor()
        {
            var carDoors = car.GetComponent<FolkloreArchives.Net.CarDoors>();
            Transform paxDoor = NearestDoorTo(car.frontPassenger != null ? car.frontPassenger.position : car.transform.position);
            yield return new WaitUntil(() => carDoors != null && paxDoor != null && carDoors.IsOpen(paxDoor));
            if (op.dog != null && op.dog.CurrentSeat != null) yield return op.dog.ExitRoutine();
            Transform dog = op.dog != null ? op.dog.transform : null;
            if (dog != null) PlaceStandingYaw(dog, dogExitPos, dogExitYaw);
        }

        Transform NearestDoorTo(Vector3 p)
        {
            Transform best = null; float bd = float.MaxValue;
            if (car != null && car.doors != null)
                foreach (var d in car.doors) { if (d == null) continue; float dd = Vector3.Distance(d.position, p); if (dd < bd) { bd = dd; best = d; } }
            return best;
        }

        // TU questline en el campamento: 1) recoger tu carpa de la cajuela; 2) ponerla donde marca
        // el FANTASMA celeste; 3) el chico pide más leña; 4) ir a buscarla y llevarla a la fogata.
        IEnumerator PlayerCampTasks()
        {
            Transform player = (op != null && op.player != null) ? op.player.transform : null;
            if (player == null) yield break;
            Vector3 cp = car.transform.position;
            Vector3 trunk = cp - car.transform.forward * 4.6f; trunk.y = GroundY(trunk, cp.y);
            Vector3 fire = _campsite != null ? _campsite.position : new Vector3(246f, cp.y, 232f);

            // 1) recoger TU carpa (morada) de atrás de la cajuela.
            yield return WaitPlayerInteract(player, trunk, playerReach, "[E] Recoger carpa");

            // 2) FANTASMA celeste/transparente donde va la carpa -> ir y ponerla.
            GameObject ghost = _playerTent != null ? MakeTentGhost(_playerTent) : null;
            Vector3 target = _playerTent != null ? _playerTent.transform.position : trunk;
            yield return WaitPlayerInteract(player, target, playerReach, "[E] Poner carpa");
            if (ghost != null) Destroy(ghost);
            if (_playerTent != null)
            {
                Vector3 full = _playerTent.transform.localScale == Vector3.zero ? Vector3.one : _playerTent.transform.localScale;
                _playerTent.transform.localScale = full * 0.05f;
                _playerTent.SetActive(true);
                yield return PopScale(_playerTent, full);
            }

            // 3) ir a HABLAR con el NEGRO (parado al lado de su carpa) -> te pide ayuda con la leña.
            yield return WaitPlayerInteract(player, greenStandPos, playerReach, "[E] Hablar con tu amigo");
            yield return SayFor("¿Me ayudás a buscar leña para la fogata?", 3.5f);

            // 4) RECIÉN AHÍ: el negro va a buscar leña al MISMO punto (el tuyo), la trae a la fogata
            //    y DESPUÉS se sienta. MaleCasual y la chica ya están sentados. Vos también juntás ahí.
            Transform negro = op != null ? op.friendMaleGreenJkt : null;
            if (negro != null) StartCoroutine(NegroFetchThenSit(negro, fire));

            // 5) vos también juntás leña en ese punto y la llevás a la fogata.
            yield return WaitPlayerInteract(player, woodPlayerPos, playerReach, "[E] Juntar leña");
            var log = MakeCarriedLog();
            log.transform.SetParent(player, false);
            log.transform.localPosition = new Vector3(0f, 1.1f, 0.5f);
            log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            yield return WaitPlayerInteract(player, fire, playerReach, "[E] Dejar la leña en la fogata");
            if (log != null)
            {
                log.transform.SetParent(null, true);
                Vector3 fp = fire; fp.y = GroundY(fire, fire.y);
                log.transform.position = fp + new Vector3(-0.35f, 0.1f, 0.2f);
                log.transform.rotation = Quaternion.Euler(0f, -25f, 90f);
            }
            // (próximo: noche -> comer/hablar -> dormir -> Rufus ve la Luz Mala)
        }

        // espera a que el jugador esté CERCA de 'pos' y apriete E. Mientras esté cerca, muestra 'hint'.
        IEnumerator WaitPlayerInteract(Transform player, Vector3 pos, float reach, string hint)
        {
            while (true)
            {
                bool near = Flat2(player.position, pos) <= reach;
                _playerHint = near ? hint : null;
                var kb = Keyboard.current;
                if (near && kb != null && kb[Key.E].wasPressedThisFrame) { _playerHint = null; yield break; }
                yield return null;
            }
        }

        // muestra una línea de diálogo (abajo) por 'secs' segundos.
        IEnumerator SayFor(string text, float secs)
        {
            _playerSay = text;
            yield return new WaitForSeconds(secs);
            _playerSay = "";
        }

        // clon TRANSLÚCIDO celeste de la carpa, para marcar DÓNDE ponerla (sin collider).
        GameObject MakeTentGhost(GameObject tent)
        {
            var ghost = Instantiate(tent);
            ghost.name = "CarpaFantasma";
            ghost.transform.SetParent(null, true);
            ghost.transform.position = tent.transform.position;
            ghost.transform.rotation = tent.transform.rotation;
            ghost.transform.localScale = tent.transform.localScale == Vector3.zero ? Vector3.one : tent.transform.localScale;
            ghost.SetActive(true);
            foreach (var col in ghost.GetComponentsInChildren<Collider>(true)) Destroy(col);
            var mat = GhostMat();
            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            return ghost;
        }

        static Material _ghostMat;
        static Material GhostMat()
        {
            if (_ghostMat != null) return _ghostMat;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            Color c = new Color(0.45f, 0.82f, 1f, 0.35f); // celeste transparente
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            _ghostMat = m;
            return m;
        }

        GUIStyle _sayStyle;
        void OnGUI()
        {
            if (!string.IsNullOrEmpty(_playerHint)) InteractHint.Draw(_playerHint);
            if (!string.IsNullOrEmpty(_playerSay))
            {
                if (_sayStyle == null) _sayStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _sayStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 300f, Screen.height - 130f, 600f, 56f), _playerSay, _sayStyle);
            }
        }

        // El negro camina a greenTentPos, arma AHÍ su carpa (recién cuando LLEGA), y después queda
        // PARADO en su tronco esperando a que el jugador le hable (NO se sienta todavía).
        IEnumerator GreenTentThenStand(Transform green, GameObject tent, Vector3 fire)
        {
            // se para del lado por donde VIENE (el auto), justo ANTES de la carpa, MIRÁNDOLA. Así
            // frena adelante y no se pasa de largo (antes venía del lado de la fogata = opuesto).
            Vector3 fromCar = car.transform.position - greenTentPos; fromCar.y = 0f;
            fromCar = fromCar.sqrMagnitude < 0.01f ? -Fwd(greenTentYaw) : fromCar.normalized;
            Vector3 standFront = greenTentPos + fromCar * 0.55f; standFront.y = GroundY(standFront, greenTentPos.y);
            float tw = 0f;
            while (green != null && Flat2(green.position, standFront) > 0.25f && tw < 25f) { StepToward(green, standFront, 2.2f); tw += Time.deltaTime; yield return null; }
            FaceTarget(green, greenTentPos);
            yield return new WaitForSeconds(0.5f);   // plantado, mirando el lugar

            float dn = green != null ? Flat2(green.position, greenTentPos) : -1f;
            string tn = tent != null ? tent.name : "NULL";
            Debug.Log($"<color=orange>[Camp] NEGRO llego (d={dn:0.0}m) -> arma '{tn}' en {greenTentPos} (t={Time.time:0.0}s)</color>");
            // aparece la carpa DELANTE de él.
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

            yield return new WaitForSeconds(0.9f);   // se queda mirando la carpa ya armada
            // RECIÉN AHORA se mueve al punto AL LADO de la carpa (greenStandPos) y queda esperando.
            if (green != null) StartCoroutine(WalkNpcTo(green, greenStandPos, greenStandPos + Fwd(greenStandYaw), null));
        }

        // (lo llama PlayerCampTasks tras el diálogo) el negro va a buscar leña y RECIÉN AHÍ se sienta.
        IEnumerator NegroFetchThenSit(Transform negro, Vector3 fire)
        {
            yield return NpcFetchWoodTo(negro, woodPlayerPos, fire, new Vector3(1.3f, 0f, 0.5f));
            if (negro == null) yield break;
            StartCoroutine(WalkNpcTo(negro, greenSitPos, fire, null));
            float t = 0f; while (t < 14f && Flat2(negro.position, greenSitPos) > 1.0f) { t += Time.deltaTime; yield return null; }
            Vector3 d = fire - greenSitPos; float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            PlaceSeated(negro, greenSitPos, yaw);
        }

        // La chica y el chico caminan a tentPairPos y arman AHÍ su carpa. Después: la chica va al
        // tronco y se sienta; el chico va a buscar leña y la lleva a la fogata.
        IEnumerator PairTentThenTasks(Transform chico, Transform chica, GameObject tent, Vector3 fire)
        {
            // se paran ENFRENTE del lugar de la carpa (del lado de la fogata), flanqueándola y
            // MIRÁNDOLA. Los muevo a los DOS desde acá y salgo cuando AMBOS están a <=0.4m.
            // la pareja se para en pairStandPos (punto exacto del owner). Los separo PERPENDICULAR
            // al eje pareja-carpa, así los DOS quedan a la misma distancia del punto (no uno lejos).
            Vector3 toTent = tentPairPos - pairStandPos; toTent.y = 0f;
            toTent = toTent.sqrMagnitude < 0.01f ? Fwd(pairStandYaw) : toTent.normalized;
            Vector3 lateral = Vector3.Cross(Vector3.up, toTent).normalized * 0.45f;
            Vector3 chicoDest = pairStandPos - lateral; chicoDest.y = GroundY(chicoDest, pairStandPos.y);
            Vector3 chicaDest = pairStandPos + lateral; chicaDest.y = GroundY(chicaDest, pairStandPos.y);
            float t = 0f;
            while (t < 30f)
            {
                bool chicoNear = chico == null || Flat2(chico.position, chicoDest) <= 0.25f;
                bool chicaNear = chica == null || Flat2(chica.position, chicaDest) <= 0.25f;
                if (chicoNear && chicaNear) break;
                if (chico != null && !chicoNear) StepToward(chico, chicoDest, 2.2f);
                if (chica != null && !chicaNear) StepToward(chica, chicaDest, 2.2f);
                t += Time.deltaTime;
                yield return null;
            }
            FaceTarget(chico, tentPairPos); FaceTarget(chica, tentPairPos);   // miran la carpa
            yield return new WaitForSeconds(0.5f);   // plantados, mirando -> ahora aparece la carpa

            float dpc = chico != null ? Flat2(chico.position, pairStandPos) : -1f;
            float dph = chica != null ? Flat2(chica.position, pairStandPos) : -1f;
            string tnp = tent != null ? tent.name : "NULL";
            Vector3 tentVis = tent != null ? tent.transform.position : Vector3.zero;
            Debug.Log($"<color=cyan>[Camp] PAREJA al PUNTO (chico d={dpc:0.0}m chica d={dph:0.0}m) -> SetActive '{tnp}'. carpa.transform en {tentVis} (t={Time.time:0.0}s)</color>");
            // aparece la carpa DELANTE de ellos.
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

            yield return new WaitForSeconds(0.9f);   // se quedan mirando la carpa ya armada
            // la chica va al tronco y se sienta; el chico (MaleCasual) se sienta AL LADO de ella.
            if (chica != null) StartCoroutine(SitOnLog(chica));
            if (chico != null) StartCoroutine(SitNextToChica(chico));
        }

        // MaleCasual camina al tronco de la chica y se sienta a su lado.
        IEnumerator SitNextToChica(Transform chico)
        {
            Vector3 sitPos = chicaSitPos + Right(chicaSitYaw) * 0.9f;
            bool a = false;
            StartCoroutine(WalkNpcTo(chico, sitPos, sitPos + Fwd(chicaSitYaw), () => a = true));
            float t = 0f; while (!a && t < 12f) { t += Time.deltaTime; yield return null; }
            PlaceSeated(chico, sitPos, chicaSitYaw);
        }

        // camina la chica al tronco y la deja SENTADA (pose seated), mirando chicaSitYaw.
        IEnumerator SitOnLog(Transform chica)
        {
            bool a = false;
            StartCoroutine(WalkNpcTo(chica, chicaSitPos, chicaSitPos + Fwd(chicaSitYaw), () => a = true));
            float t = 0f; while (!a && t < 10f) { t += Time.deltaTime; yield return null; }
            PlaceSeated(chica, chicaSitPos, chicaSitYaw);
        }

        // un NPC va a 'woodAt' (con 'offset' para no encimarse), "junta" leña (tronquito en sus
        // manos), la lleva a la fogata y la deja. Se para si estaba sentado.
        IEnumerator NpcFetchWoodTo(Transform npc, Vector3 woodAt, Vector3 fire, Vector3 offset)
        {
            if (npc == null) yield break;
            var anim = npc.GetComponent<HumanWalkAnim>(); if (anim != null) anim.seated = false; // por si estaba sentado
            bool a = false;
            StartCoroutine(WalkNpcTo(npc, woodAt + offset, woodAt, () => a = true));
            float t = 0f; while (!a && t < 16f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.9f);   // se agacha / junta

            var log = MakeCarriedLog();
            log.transform.SetParent(npc, false);
            log.transform.localPosition = new Vector3(0f, 1.15f, 0.4f);
            log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            bool b = false;
            StartCoroutine(WalkNpcTo(npc, fire + offset, fire, () => b = true));
            t = 0f; while (!b && t < 16f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.5f);

            // dejar la leña en la fogata.
            log.transform.SetParent(null, true);
            Vector3 fp = fire; fp.y = GroundY(fire, fire.y);
            log.transform.position = fp + offset * 0.3f + new Vector3(0f, 0.1f, 0f);
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
        // La pose seated SUBE el modelo -seatedModelDrop (0.63m, calibrado para el asiento del auto),
        // así que en el tronco levitaban. Compenso bajando el transform esa misma cantidad para que
        // queden APOYADOS en el tope del tronco (GroundY cae en el collider del tronco).
        void PlaceSeated(Transform t, Vector3 pos, float yaw)
        {
            if (t == null) return;
            var anim = t.GetComponent<HumanWalkAnim>();
            var cc = t.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            pos.y = GroundY(pos, pos.y, t) + seatYOffset;   // bajar para que quede apoyado (no levitando)
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = was;
            if (anim != null) anim.seated = true;
        }

        // direcciones planas desde un yaw (grados). Unity: forward=(sin,0,cos), right=(cos,0,-sin).
        static Vector3 Fwd(float yaw)   { float r = yaw * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)); }
        static Vector3 Right(float yaw) { float r = yaw * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r)); }

        // gira 'tr' para mirar 'target' (plano).
        static void FaceTarget(Transform tr, Vector3 target)
        {
            if (tr == null) return;
            Vector3 look = target - tr.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) tr.rotation = Quaternion.LookRotation(look.normalized);
        }

        // camina un NPC hasta 'dest' (sin armar carpa), lo deja mirando 'faceTarget', y avisa 'done'.
        IEnumerator WalkNpcTo(Transform npc, Vector3 dest, Vector3 faceTarget, System.Action done)
        {
            if (npc == null) { done?.Invoke(); yield break; }
            float t = 0f;
            while (Flat2(npc.position, dest) > 0.5f && t < 20f)
            {
                StepToward(npc, dest, 2.2f);
                t += Time.deltaTime;
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
            if (trunk == null) { if (open) Debug.LogWarning("[Camp] no encontré la cajuela -> mirá el log de partes."); return; }
            // NO uso CarDoors (abre girando sobre eje vertical = se corre al costado). Le pongo un
            // TrunkLid: se LEVANTA (bisagra horizontal) y ADEMÁS el jugador la abre/cierra con E.
            var lid = trunk.GetComponent<TrunkLid>();
            if (lid == null)
            {
                lid = trunk.gameObject.AddComponent<TrunkLid>();
                lid.car = car.transform;
                lid.openDeg = trunkOpenDeg;
            }
            lid.SetOpen(open);
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
            // esquivar el AUTO y a los OTROS personajes (no subirse al auto, no pisarse): si hay algo
            // adelante, se rodea hacia el lado libre. Sondeo corto para no oscilar cerca del destino.
            dir = AvoidDir(tr, pos, dir, Mathf.Min(1.1f, d));
            Vector3 np = pos + dir * Mathf.Min(speed * Time.deltaTime, d);
            np.y = GroundY(np, pos.y, tr);   // <-- pasar 'tr' para que el raycast NO se pegue a su propio collider
            tr.position = np;
            tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }

        // devuelve una dirección de avance que RODEA obstáculos (auto/otros personajes). Prueba
        // ángulos crecientes a ambos lados y elige el primer lado libre.
        static Vector3 AvoidDir(Transform self, Vector3 pos, Vector3 dir, float probe)
        {
            if (probe < 0.5f || !BlockedAhead(self, pos, dir, probe)) return dir;
            for (int deg = 35; deg <= 90; deg += 25)
            {
                Vector3 r = Quaternion.Euler(0f, deg, 0f) * dir;
                if (!BlockedAhead(self, pos, r, probe)) return r;
                Vector3 l = Quaternion.Euler(0f, -deg, 0f) * dir;
                if (!BlockedAhead(self, pos, l, probe)) return l;
            }
            return dir; // todo bloqueado: seguir derecho (mejor que trabarse)
        }

        // ¿hay un obstáculo (auto u otro personaje) adelante, dentro de 'dist'?
        static bool BlockedAhead(Transform self, Vector3 pos, Vector3 dir, float dist)
        {
            Vector3 origin = pos + Vector3.up * 0.8f;
            var hits = Physics.SphereCastAll(origin, 0.35f, dir.normalized, dist);
            foreach (var h in hits)
                if (IsObstacle(h.collider, self)) return true;
            return false;
        }

        // cuenta como obstáculo SOLO el AUTO (para no subirse encima). Esquivar a los otros
        // personajes hacía que se trabaran/oscilaran y no llegaran a los puntos -> se saca.
        static bool IsObstacle(Collider col, Transform self)
        {
            if (col == null || col is TerrainCollider) return false;
            if (self != null && (col.transform == self || col.transform.IsChildOf(self) || self.IsChildOf(col.transform))) return false;
            return col.GetComponentInParent<CarController>() != null;
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
