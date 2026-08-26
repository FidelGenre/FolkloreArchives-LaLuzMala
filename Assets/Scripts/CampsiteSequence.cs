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
using UnityEngine.UI;

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
        public Vector3 greenSleepPos = new Vector3(240.9831f, 22.69508f, 236.8229f); // el negro se acuesta ADENTRO de su carpa (owner)
        public float   greenSleepYaw = 134.933f;
        public float   seatYOffset  = -1.3f;   // ajuste de altura al sentarse en el tronco (m). Más negativo = más abajo.

        [Header("Cajuela")]
        public float trunkOpenDeg = 70f;    // se abre girando sobre eje HORIZONTAL (se levanta). + = arriba, - = abajo.

        [Header("Tu parte: carpa (morada) + leña")]
        public Vector3 woodPlayerPos = new Vector3(229.8513f, 24.00745f, 231.7843f); // TODOS buscan la leña acá (casual, negro y vos)
        public float   playerReach   = 3.2f;   // distancia para tus interacciones con E

        [Header("Sentarse a la fogata + noche (owner)")]
        public Vector3 playerSitPos = new Vector3(243.3525f, 23.79453f, 232.117f); // el jugador se sienta en este tronco (Rufus al lado)
        public float   playerSitYaw = 84.533f;
        public Vector3 nightCamPos  = new Vector3(234.8221f, 26.89305f, 222.9142f); // cámara de la noche (24.593 + 2.30 de altura de ojos)
        public float   nightCamYaw  = 60.933f;
        public float   dogFireScaleMul = 0.4f;   // qué tan chico queda Rufus sentado en el tronco (menor = más chico)
        Transform _dogModel; Vector3 _dogModelScaleSaved;
        GameObject _poop;   // la caca de Rufus (se limpia a la mañana siguiente)
        public Vector3 towerLightPos = new Vector3(352.3531f, 51.31269f, 219.8981f); // binoculares parpadeando en la torre (owner)

        [Header("Escena nocturna: Rufus + Luz Mala (owner)")]
        public Vector3 luzMalaPos = new Vector3(194.1414f, 23.9108f, 254.7851f); // la Luz Mala aparece acá (lago, de lejos)
        public Vector3 dogPoopPos = new Vector3(242.588f, 23.2307f, 219.621f);    // Rufus va a cagar acá (owner)
        public Vector3 dogBarkPos = new Vector3(231.419f, 24.38922f, 240.93f);    // Rufus se para acá a ladrarle a la luz (owner)

        // ---- rancho de la vieja (misión de las cañas) ----
        public Vector3 houseDoorPos = new Vector3(136.1347f, 27.24684f, 125.4351f); // puerta de la casa: tocás acá (owner TEST_PLAYER)
        public float   houseDoorYaw = -178.982f;
        public Vector3 corralGateStand = new Vector3(116.6176f, 26.97f, 149.8931f);  // parado acá para abrir la tranquera (owner)
        public Vector3 sheepPasturePos = new Vector3(124.067f, 26.1109f, 167.3692f); // las ovejas van a pastar acá (owner)
        LuzMala _luzMala;
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
            // la Luz Mala arranca DESACTIVADA (si no, aparecería sola de noche); la controla la
            // escena nocturna (aparece en el lago cuando Rufus termina de cagar).
            _luzMala = Object.FindFirstObjectByType<LuzMala>();
            if (_luzMala != null) _luzMala.gameObject.SetActive(false);
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

            // 6) recién cuando el negro TAMBIÉN dejó su leña -> aparece la opción de PRENDER.
            _playerHint = "Esperá a que traigan la leña...";
            yield return new WaitUntil(() => _negroWoodDone);
            _playerHint = null;
            yield return WaitPlayerInteract(player, fire, playerReach, "[E] Prender la fogata");
            SetCampfireLit(true);

            // 7) el jugador y Rufus se sientan en el tronco; una cámara muestra cómo se hace de
            //    NOCHE de forma SUAVE (no un corte). (próximo: comer/hablar -> dormir -> Luz Mala)
            yield return SitAtFireAndNight(player);
        }

        // el jugador camina al tronco y se sienta, Rufus queda al lado, y una cámara fija muestra
        // la transición SUAVE tarde->noche.
        IEnumerator SitAtFireAndNight(Transform player)
        {
            PartyController.CinematicLock = true;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            float tw = 0f;
            while (Flat2(player.position, playerSitPos) > 0.4f && tw < 12f) { StepToward(player, playerSitPos, 2.0f); tw += Time.deltaTime; yield return null; }
            PlaceSeated(player, playerSitPos, playerSitYaw);

            // Rufus al lado (no tiene pose "sentado en tronco": lo pongo al lado, en el piso). Lo
            // ACHICO para el tronco (aparecía gigante); se restaura cuando se despierta a cagar.
            Transform dog = op != null && op.dog != null ? op.dog.transform : null;
            if (dog != null)
            {
                PlaceStandingYaw(dog, playerSitPos + Right(playerSitYaw) * 0.9f, playerSitYaw);
                _dogModel = dog.Find("Model");
                if (_dogModel != null) { _dogModelScaleSaved = _dogModel.localScale; _dogModel.localScale = _dogModelScaleSaved * dogFireScaleMul; }
            }

            // cámara fija de la noche.
            MakeNightCam();

            // VISTA EXTENDIDA durante TODA la charla (se ve lejos, se llega a ver la torre a ~106m).
            var party = Object.FindFirstObjectByType<PartyController>();
            Camera pcam = party != null ? party.personCam : null;
            if (pcam != null) pcam.farClipPlane = 260f;
            RenderSettings.fogDensity = 0.010f;

            // se oscurece SUAVE pero MANTENIENDO la vista abierta (oscurece sol/ambiente/cielo,
            // NO sube la niebla ni cierra el clip). Corre en paralelo con la charla.
            var dn = Object.FindFirstObjectByType<DayNightController>();
            if (dn != null) StartCoroutine(DarkenKeepingView(dn, 12f));

            // CHARLA junto al fuego (cuentan cosas, se ríen) mientras se hace de noche.
            yield return SayFor("¿Se acuerdan la última vez que acampamos acá?", 3.2f);
            yield return SayFor("¡Jaja sí! Cuando se te prendió fuego la campera, gordo.", 3.4f);
            yield return SayFor("Eh, casi me muero y ustedes cagados de risa...", 3.2f);

            // a mitad de la charla, la cámara pasa a 1ª PERSONA (seguís sentado).
            SwitchToPlayerCamSeated();

            yield return SayFor("Igual qué lindo quedó el campamento, ¿no?", 3.2f);
            yield return SayFor("Sí, tranqui. Una noche perfecta.", 3.0f);

            // LUZ BLANCA FIJA de unos binoculares desde la torre (alguien los observa).
            var beacon = MakeBlinkingLight(towerLightPos);
            yield return SayFor("...¿vieron esa luz blanca en la torre?", 2.6f);
            yield return SayFor("Es fija... como si alguien nos mirara con binoculares.", 3.2f);
            // el jugador y Rufus se asustan; los otros LOS cargan por asustadizos.
            yield return SayFor("(Rufus gruñe y se te pega, erizado)", 2.6f);
            yield return SayFor("Uy, arrancaron ustedes dos... ¡par de faloperos!", 3.2f);
            yield return SayFor("No fue nada, vayan a dormir. Mañana seguimos.", 3.0f);
            if (beacon != null) Destroy(beacon);

            // todos se levantan de los troncos y se van a DORMIR (acostados dentro de sus carpas).
            yield return EveryoneToSleep();
        }

        // luz BLANCA FIJA (reflejo de unos binoculares) en la torre: luz puntual (glow local) + una
        // esfera BLANCA unlit para que se VEA el punto desde el campamento (a ~106m). No parpadea.
        GameObject MakeBlinkingLight(Vector3 pos)
        {
            var go = new GameObject("BinocularesTorre");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Color.white;
            l.intensity = 8f;
            l.range = 30f;

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Glint";
            dot.transform.SetParent(go.transform, false);
            dot.transform.localScale = Vector3.one * 0.6f;
            var col = dot.GetComponent<Collider>(); if (col != null) Destroy(col);
            var r = dot.GetComponent<Renderer>();
            if (r != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                var m = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                m.color = Color.white; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                r.sharedMaterial = m;
            }
            return go;
        }

        // todos se paran y van a acostarse DENTRO de su carpa.
        IEnumerator EveryoneToSleep()
        {
            RemoveSeatedLook();   // el jugador se levanta -> se saca el free-look sentado
            // cerrar la noche del todo (niebla/clip normales de noche + fase Night para la Luz Mala).
            var dn = Object.FindFirstObjectByType<DayNightController>();
            if (dn != null) dn.SetPhase(DayNightController.Phase.Night);

            Transform player = op != null && op.player != null ? op.player.transform : null;
            Transform casual = op != null ? op.friendMaleCasual  : null;
            Transform green  = op != null ? op.friendMaleGreenJkt : null;
            Transform chica  = op != null ? op.friendFemaleSec    : null;

            StartCoroutine(SleepInTent(casual, tentPairPos, tentPairYaw, Right(tentPairYaw) * -0.35f));
            StartCoroutine(SleepInTent(chica,  tentPairPos, tentPairYaw, Right(tentPairYaw) *  0.35f));
            StartCoroutine(SleepInTent(green,  greenSleepPos, greenSleepYaw, Vector3.zero));

            Vector3 pTent = _playerTent != null ? _playerTent.transform.position : playerSitPos;
            float pYaw = _playerTent != null ? _playerTent.transform.eulerAngles.y : playerSitYaw;
            yield return SleepInTent(player, pTent, pYaw, Vector3.zero);

            // Rufus se echa al lado del jugador en la carpa (después se levanta a cagar).
            Transform dog = op != null && op.dog != null ? op.dog.transform : null;
            if (dog != null) PlaceStandingYaw(dog, pTent + Right(pYaw) * 0.7f, pYaw);

            // ESCENA NOCTURNA: se cambia a Rufus, se levanta a cagar, ve la Luz Mala en el lago,
            // ladra -> se va, y el jugador se despierta y lo lleva a dormir.
            yield return DogNightScene(pTent, pYaw);
        }

        // Rufus (lo controlás) se levanta, va a cagar, ve la Luz Mala en el lago, ladra -> se va;
        // el jugador se despierta y lo lleva de nuevo a la carpa.
        IEnumerator DogNightScene(Vector3 playerTent, float playerYaw)
        {
            Transform dog = op != null && op.dog != null ? op.dog.transform : null;
            Transform player = op != null && op.player != null ? op.player.transform : null;
            if (dog == null) yield break;

            yield return new WaitForSeconds(1.5f);   // duermen un rato

            // 1) CONTROL a Rufus (LIBRE: te movés vos, cámara del perro). Se levanta de al lado tuyo.
            if (_dogModel != null) _dogModel.localScale = _dogModelScaleSaved;   // vuelve a su tamaño normal (ya no está en el tronco)
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null) party.ForceControl(true);
            PartyController.CinematicLock = false;   // te movés vos
            var dcc = dog.GetComponent<CharacterController>(); if (dcc != null) dcc.enabled = true;

            // 2) llevá a Rufus al lugar y "[E] Cagar" (te movés hasta ahí).
            yield return WaitPlayerInteract(dog, dogPoopPos, 2.8f, "[E] Cagar");
            _poop = MakePoop(dog.position + dog.forward * -0.35f);
            _playerHint = null;
            yield return new WaitForSeconds(1.2f);

            // 3) al cagar, aparece la LUZ MALA EN FRENTE (en el lago). Andá hacia ella (seguís vos).
            if (_luzMala != null)
            {
                _luzMala.transform.position = luzMalaPos;
                _luzMala.holdStill = true;
                _luzMala.gameObject.SetActive(true);
            }
            _playerHint = "Una luz en el lago... acercate";

            // 4) cuando LLEGÁS al punto (frente a la luz), Rufus se PAUSA y LADRA solo -> la luz se va.
            float guard = 0f;
            while (guard < 60f && Flat2(dog.position, dogBarkPos) > 2.5f) { guard += Time.deltaTime; yield return null; }
            _playerHint = null;
            PartyController.CinematicLock = true;   // Rufus se pausa (no lo movés)
            float barkYaw = Mathf.Atan2(luzMalaPos.x - dogBarkPos.x, luzMalaPos.z - dogBarkPos.z) * Mathf.Rad2Deg;
            PlaceStandingYaw(dog, dogBarkPos, barkYaw);   // parado en el punto, mirando la luz
            var da = dog.GetComponent<DogAudio>();
            if (da != null) da.Bark();
            yield return new WaitForSeconds(0.7f);
            if (da != null) da.Bark();
            if (_luzMala != null) _luzMala.gameObject.SetActive(false);   // se va al ladrar
            yield return new WaitForSeconds(0.9f);

            // 5) se despierta la persona: se PARA al lado de la carpa y te reta. SEGUÍS con Rufus
            //    (su cámara) -> llevalo vos de vuelta a la carpa.
            if (player != null)
            {
                var pAnim = player.GetComponent<HumanWalkAnim>(); if (pAnim != null) pAnim.seated = false;
                PlaceStandingYaw(player, playerTent + Right(playerYaw) * 1.2f, playerYaw);   // parado al lado de la carpa
            }
            PartyController.CinematicLock = false;   // te movés con Rufus (cámara del perro)
            _playerSay  = "¡Rufus! ¿Qué hacés ahí? Vení, a dormir.";   // te reta mientras volvés
            _playerHint = "Volvé a la carpa";

            // 6) cuando Rufus LLEGA a la carpa (mantuvo la cámara hasta acá), AMBOS entran y se
            //    acuestan (scripteado).
            float back = 0f;
            while (back < 60f && Flat2(dog.position, playerTent) > 1.6f) { back += Time.deltaTime; yield return null; }
            _playerSay = ""; _playerHint = null;
            PartyController.CinematicLock = true;
            if (player != null) { var pcc = player.GetComponent<CharacterController>(); if (pcc != null) pcc.enabled = false; }
            if (dcc != null) dcc.enabled = false;
            Vector3 dogSpot = playerTent + Right(playerYaw) * 0.7f;
            float enter = 0f;
            while (enter < 6f && (Flat2(dog.position, dogSpot) > 0.4f || (player != null && Flat2(player.position, playerTent) > 0.4f)))
            {
                if (player != null) StepToward(player, playerTent, 2.2f);
                StepToward(dog, dogSpot, 2.2f);
                enter += Time.deltaTime;
                yield return null;
            }
            if (player != null) PlaceLyingInTent(player, playerTent, playerYaw);
            PlaceLyingInTent(dog, dogSpot, playerYaw);
            yield return new WaitForSeconds(1.0f);

            // 7) AMANECE lento (mismo plano del campamento) + parpadeo dentro de la carpa + aparecen
            //    afuera -> arranca el NUEVO DÍA y te movés libremente.
            yield return WakeNewDay(playerTent, playerYaw, player, dog);

            // 8) mañana: limpiás la caca, hablás con el malecasual (pesca / faltan las cañas) y
            //    arrancan LIBRES al rancho de la vieja a pedir prestadas unas cañas.
            yield return MorningAfterWake();
        }

        // Mañana siguiente: el malecasual está PARADO afuera de su carpa; la chica y el negro
        // sentados en los troncos. Limpiás la caca de Rufus, hablás sobre ir a pescar, no están
        // las cañas en el auto, el malecasual los putea y deciden ir a un rancho cercano a pedir
        // prestado. Al final te suelta el control (caminás vos + Rufus, NO scripteado).
        IEnumerator MorningAfterWake()
        {
            Transform player = op != null && op.player != null ? op.player.transform : null;
            Transform casual = op != null ? op.friendMaleCasual  : null;
            Transform chica  = op != null ? op.friendFemaleSec   : null;
            Transform negro  = op != null ? op.friendMaleGreenJkt : null;
            if (player == null) yield break;
            Vector3 fire = _campsite != null ? _campsite.position : new Vector3(246f, 23f, 232f);

            // NPCs despiertos: malecasual PARADO afuera de su carpa; chica y negro en los troncos.
            if (casual != null) PlaceStandingYaw(casual, pairStandPos, pairStandYaw);
            if (chica  != null) PlaceSeated(chica, chicaSitPos, chicaSitYaw);
            if (negro  != null) { float ny = Mathf.Atan2(fire.x - greenSitPos.x, fire.z - greenSitPos.z) * Mathf.Rad2Deg; PlaceSeated(negro, greenSitPos, ny); }

            // control tuyo (persona, 1ª persona) y libre.
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null) party.ForceControl(false);
            PartyController.CinematicLock = false;

            // 1) LIMPIAR la caca de Rufus.
            if (_poop != null)
            {
                yield return WaitPlayerInteract(player, _poop.transform.position, playerReach, "[E] Limpiar la caca de Rufus");
                if (_poop != null) { Destroy(_poop); _poop = null; }
                yield return SayFor("Listo... qué asco, Rufus.", 2.2f);
            }

            // 2) hablar con el MALECASUAL (parado afuera de su carpa).
            _playerHint = "Hablá con tu amigo";
            yield return WaitPlayerInteract(player, pairStandPos, playerReach + 0.6f, "[E] Hablar");
            _playerHint = null;
            if (casual != null) FaceTarget(casual, player.position);
            yield return SayFor("¡Buen día! Hoy vamos a pescar, hay un lago acá cerca.", 3.2f);
            yield return SayFor("¿Trajeron las cañas?", 2.4f);
            yield return SayFor("Eh... deben estar en el auto. Fijate.", 2.8f);

            // 3) ir al AUTO a buscar las cañas -> no están.
            _playerHint = "Fijate las cañas en el auto";
            yield return WaitPlayerInteract(player, car != null ? car.transform.position : fire, playerReach + 1.5f, "[E] Revisar el auto");
            _playerHint = null;
            yield return SayFor("No están... nos olvidamos las cañas.", 2.8f);

            // 4) volver con el malecasual.
            _playerHint = "Volvé con tu amigo";
            yield return WaitPlayerInteract(player, pairStandPos, playerReach + 0.6f, "[E] Hablar");
            _playerHint = null;
            if (casual != null) FaceTarget(casual, player.position);
            yield return SayFor("Son unos pelotudos... No pierden la cabeza porque la tienen pegada.", 3.8f);
            yield return SayFor("De última le preguntamos: vimos un rancho acá cerca.", 3.2f);
            yield return SayFor("Capaz nos presta unas cañas.", 2.4f);
            yield return SayFor("Bueno, dale.", 1.8f);

            // 5) LIBRE: te vas caminando (vos + Rufus) al rancho de la vieja. NO scripteado.
            yield return GoToRanchoFlow();
        }

        // Desde acá arranca tanto el flujo NORMAL (después de la charla en el campamento, arriba)
        // como el checkpoint de DEBUG "Rancho (cañas)" (BeginAtRancho, más abajo): caminás LIBRE
        // hasta la puerta de la casa, no atienden, vas al baño del granero -> RanchoBathroomScene.
        public IEnumerator GoToRanchoFlow()
        {
            Transform player = op != null && op.player != null ? op.player.transform : null;
            if (player == null) yield break;

            _playerHint = "Andá al rancho a pedir unas cañas";

            // al LLEGAR a la puerta de la casa tocás -> no atiende nadie -> a buscar por el granero.
            yield return WaitPlayerInteract(player, houseDoorPos, playerReach + 0.6f, "[E] Tocar la puerta");
            yield return SayFor("(Tocás la puerta...)", 1.6f);
            yield return new WaitForSeconds(1.4f);
            yield return SayFor("No atiende nadie...", 2.0f);
            yield return SayFor("Habrá alguien atrás. Vamos a ver por el granero.", 3.0f);
            _playerHint = "Buscá a alguien por el baño del granero";

            // baño del granero (letrina): tocás -> sale el viejo (susto) -> charla -> la vieja -> favor.
            yield return RanchoBathroomScene();
        }

        // ---- DEBUG checkpoint "Rancho (cañas)" ----
        // owner: "pone un nuevo checkpoint a partir de que hay que ir hasta lo de la vieja" --
        // saltea TODA la cinemática del campamento (llegada, armado de carpas, noche, Rufus/Luz
        // Mala, despertar, charla de la mañana) y arranca LIBRE justo en el punto de arriba
        // ("andá al rancho a pedir unas cañas"). El campamento aparece YA armado (auto estacionado,
        // 3 carpas puestas, fogata prendida, los 3 amigos en su lugar de mañana) para no tener que
        // rehacer toda la noche cada vez que se prueba la misión del rancho. Lo dispara
        // OpeningDriveSequence cuando el checkpoint de EditorPrefs es >= 3 (ver DebugCheckpointButtons.cs).
        public void BeginAtRancho(OpeningDriveSequence seq)
        {
            op = seq;
            car = Object.FindFirstObjectByType<CarController>();
            _luzMala = Object.FindFirstObjectByType<LuzMala>();
            if (_luzMala != null) _luzMala.gameObject.SetActive(false);
            StartCoroutine(SetupAtRanchoCheckpoint());
        }

        IEnumerator SetupAtRanchoCheckpoint()
        {
            yield return null;   // mismo motivo que Run(): esperar el Start() de PlayerVehicleInteractor

            // el auto queda estacionado en el campamento (como al final de la llegada), vacío.
            if (car != null)
            {
                Vector3 parkW = new Vector3(campParkXZ.x, campParkY, campParkXZ.y);
                Quaternion parkRot = Quaternion.Euler(0f, campParkYaw, 0f);
                car.transform.position = parkW; car.transform.rotation = parkRot;
                var rb = car.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic) { rb.position = parkW; rb.rotation = parkRot; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                car.autoPilot = false; car.driving = false;
                car.externalThrottle = 0f; car.externalSteer = 0f;
            }

            // campamento YA armado: revela las 3 carpas en su lugar final (sin animación de pop) y
            // prende la fogata (la noche ya pasó).
            HideCampForSetup();
            foreach (var t in _tents) { if (t == null) continue; t.transform.localScale = Vector3.one; t.SetActive(true); }
            var npcTents = new List<GameObject>();
            foreach (var t in _tents) if (t != null && t != _playerTent) npcTents.Add(t);
            // mismo swap que DisembarkAndWalk: npcTents[1] = pareja, npcTents[0] = negro.
            GameObject tentPair  = npcTents.Count > 1 ? npcTents[1] : null;
            GameObject tentNegro = npcTents.Count > 0 ? npcTents[0] : null;
            if (tentPair != null)
            {
                Vector3 tp = tentPairPos; tp.y = GroundY(tp, tp.y);
                tentPair.transform.position = tp; tentPair.transform.rotation = Quaternion.Euler(0f, tentPairYaw, 0f);
            }
            if (tentNegro != null)
            {
                Vector3 tp = greenTentPos; tp.y = GroundY(tp, tp.y);
                tentNegro.transform.position = tp; tentNegro.transform.rotation = Quaternion.Euler(0f, greenTentYaw, 0f);
            }
            SetCampfireLit(true);

            // los 3 amigos: FUERA del auto (ya bajaron hace rato) y en su lugar de la MAÑANA
            // (mismas posiciones que arma MorningAfterWake).
            Transform casual = op != null ? op.friendMaleCasual  : null;
            Transform chica  = op != null ? op.friendFemaleSec   : null;
            Transform negro  = op != null ? op.friendMaleGreenJkt : null;
            UnparentFriend(casual); UnparentFriend(chica); UnparentFriend(negro);
            Vector3 fire = _campsite != null ? _campsite.position : new Vector3(246f, 23f, 232f);
            if (casual != null) PlaceStandingYaw(casual, pairStandPos, pairStandYaw);
            if (chica  != null) PlaceSeated(chica, chicaSitPos, chicaSitYaw);
            if (negro  != null) { float ny = Mathf.Atan2(fire.x - greenSitPos.x, fire.z - greenSitPos.z) * Mathf.Rad2Deg; PlaceSeated(negro, greenSitPos, ny); }

            // vos + Rufus, PARADOS y libres, afuera de tu carpa (mañana ya, caca de Rufus ya limpia).
            Transform player = op != null && op.player != null ? op.player.transform : null;
            Transform dog    = op != null && op.dog != null    ? op.dog.transform    : null;
            Vector3 pTent = _playerTent != null ? _playerTent.transform.position : playerSitPos;
            float pYaw = _playerTent != null ? _playerTent.transform.eulerAngles.y : playerSitYaw;
            Vector3 outside = pTent + Fwd(pYaw) * 2.2f;
            if (player != null)
            {
                var pvi = player.GetComponent<PlayerVehicleInteractor>();
                if (pvi != null && pvi.CurrentSeat != null) yield return pvi.ExitRoutine();
                var pAnim = player.GetComponent<HumanWalkAnim>(); if (pAnim != null) pAnim.seated = false;
                PlaceStandingYaw(player, outside, pYaw);
                var pcc = player.GetComponent<CharacterController>(); if (pcc != null) pcc.enabled = true;
            }
            if (dog != null)
            {
                var dvi = dog.GetComponent<PlayerVehicleInteractor>();
                if (dvi != null && dvi.CurrentSeat != null) yield return dvi.ExitRoutine();
                PlaceStandingYaw(dog, outside + Right(pYaw) * 1.1f, pYaw);
                var dcc = dog.GetComponent<CharacterController>(); if (dcc != null) dcc.enabled = true;
            }

            // día (misma fase final que deja BrightenToDay tras el amanecer), sin la animación.
            var dn = Object.FindFirstObjectByType<DayNightController>();
            if (dn != null) { dn.SetNightBlend(0f); dn.SetPhase(DayNightController.Phase.Dusk); }

            // control LIBRE al jugador.
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null) party.ForceControl(false);
            PlayerVehicleInteractor.DrivingLocked = false;
            PlayerVehicleInteractor.PastGasStation = true;
            RestoreControl();

            // arranca justo en el pedido: "andá al rancho a pedir unas cañas".
            yield return GoToRanchoFlow();
        }

        // desparenta a un amigo (por si quedó sentado en el auto) y lo deja listo para pararlo/
        // sentarlo a mano -- mismo criterio que UnseatAndPlace, sin la parte de PARARLO todavía.
        static void UnparentFriend(Transform npc)
        {
            if (npc == null) return;
            npc.SetParent(null, false);
            npc.localScale = Vector3.one;
            var fw = npc.GetComponent<FriendWander>(); if (fw != null) fw.enabled = false;
            var anim = npc.GetComponent<HumanWalkAnim>(); if (anim != null) anim.seated = false;
        }

        // Escena del BAÑO del rancho: el jugador toca la puerta de la letrina, SALE EL VIEJO
        // (susto), lo confronta ("propiedad privada"), le piden una caña, el viejo despierta a
        // la vieja, ella viene y les presta las cañas a cambio de un favor: sacar las ovejas.
        // Todo referenciado por OBJETO (letrina.007 / RanchoViejo / OldLady_Storyteller), sin
        // coordenadas hardcodeadas.
        IEnumerator RanchoBathroomScene()
        {
            Transform player = op != null && op.player != null ? op.player.transform : null;
            if (player == null) yield break;
            Transform door    = FindObj("letrina.007");
            Transform oldMan  = FindObj("RanchoViejo");
            Transform oldLady = FindObj("OldLady_Storyteller");
            if (door == null) { _playerHint = null; yield break; }   // sin letrina no hay escena

            // 1) tocás la puerta del baño
            yield return WaitPlayerInteract(player, door.position, playerReach + 1.2f, "[E] Tocar la puerta");
            _playerHint = null;
            PartyController.CinematicLock = true;   // scripteado a partir de acá
            yield return SayFor("(Tocás la puerta del baño...)", 1.2f);

            // 2) SALE EL VIEJO (susto): se activa, te encara, flash negro + ladrido de Rufus
            if (oldMan != null) { oldMan.gameObject.SetActive(true); FaceTarget(oldMan, player.position); }
            var black = MakeBlackOverlay();
            var img = black != null ? black.GetComponent<RawImage>() : null;
            if (img != null) img.color = Color.black;
            yield return FadeOverlay(img, 1f, 0f, 0.5f);
            if (black != null) Destroy(black);
            var da = op.dog != null ? op.dog.GetComponent<DogAudio>() : null;
            if (da != null) da.Bark();

            // 3) confrontación + pedido de la caña
            yield return SayFor("¡¿Qué hacen acá?! ¡Esto es propiedad privada!", 3.2f);
            yield return SayFor("Perdón, señor... estamos acampando acá cerca.", 3.0f);
            yield return SayFor("¿No tendría una caña de pescar para prestarnos?", 3.2f);
            yield return SayFor("Cañas... las que usamos con mi mujer. Pregúntenle a ella, ya la despierto.", 4.0f);

            // 4) el viejo va a buscar a la vieja
            if (oldMan != null && oldLady != null)
            {
                float t = 0f;
                while (t < 8f && Flat2(oldMan.position, oldLady.position) > 1.6f) { StepToward(oldMan, oldLady.position, 2.2f); t += Time.deltaTime; yield return null; }
                FaceTarget(oldMan, oldLady.position);
            }
            yield return SayFor("(El viejo entra y despierta a la vieja...)", 2.0f);

            // 5) la vieja viene hacia el jugador
            if (oldLady != null)
            {
                float t = 0f;
                while (t < 14f && Flat2(oldLady.position, player.position) > 2.4f) { StepToward(oldLady, player.position, 2.0f); t += Time.deltaTime; yield return null; }
                FaceTarget(oldLady, player.position);
            }
            yield return SayFor("Buenas... mucho gusto. ¿Qué necesitan, chicos?", 3.2f);
            yield return SayFor("Unas cañas para pescar, nos las olvidamos y vinimos a acampar.", 3.6f);
            yield return SayFor("Mmm... dale, se las presto. Pero a cambio de un favor.", 3.4f);
            yield return SayFor("Sáquenme las ovejas a pastar, ¿sí? Yo ya no puedo.", 3.4f);

            PartyController.CinematicLock = false;   // volvés a moverte
            _playerHint = "Abrí la tranquera del corral para sacar las ovejas";

            // 6) abrir la TRANQUERA (la arma el botón de editor como "TranqueraCorral"). Esperamos
            //    a que la abras con E; después salen las ovejas al pastizal (falta el MODELO de oveja).
            Transform gateT = FindObj("TranqueraCorral");
            var gate = gateT != null ? gateT.GetComponent<CorralGate>() : null;
            if (gate != null)
            {
                while (!gate.IsOpen) yield return null;
                _playerHint = null;
                yield return SayFor("¡Vamos, ovejas! A pastar...", 2.2f);

                // las ovejas (grupo "Ovejas", lo pone el botón de editor) salen al pastizal
                Transform flockRoot = FindObj("Ovejas");
                if (flockRoot != null)
                {
                    var flock = new List<Transform>();
                    foreach (Transform s in flockRoot) flock.Add(s);
                    float t = 0f;
                    while (t < 22f)
                    {
                        bool all = true;
                        for (int i = 0; i < flock.Count; i++)
                        {
                            Vector3 dest = sheepPasturePos + Right(0f) * ((i - (flock.Count - 1) * 0.5f) * 1.3f);
                            if (Flat2(flock[i].position, dest) > 1.0f) { StepToward(flock[i], dest, 1.7f); all = false; }
                        }
                        if (all) break;
                        t += Time.deltaTime;
                        yield return null;
                    }
                    yield return SayFor("Listo, ya están pastando tranquilas.", 2.4f);
                }
            }
            _playerHint = "Volvé con la vieja";
            // (sigue: caja del granero (screamer) -> arreglar el baño -> mates + historia de la Luz
            //  Mala -> volver al campamento. FALTAN coordenadas.)
        }

        // busca un objeto por nombre en la escena (incluye inactivos, ej. RanchoViejo desactivado).
        static Transform FindObj(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == name) return t;
            return null;
        }

        // Despertar al día siguiente: (A) amanece LENTO con el mismo plano cenital del campamento,
        // (B) cámara mirando el techo de la carpa con un PARPADEO (abrís los ojos), (C) aparecen
        // afuera parados y te devuelve el control LIBRE (1ª persona, Rufus te sigue).
        IEnumerator WakeNewDay(Vector3 playerTent, float playerYaw, Transform player, Transform dog)
        {
            PartyController.CinematicLock = true;

            // (A) MISMO PLANO del campamento (cenital) mientras amanece de a poco.
            MakeNightCam();   // reusa _overhead en nightCamPos/nightCamYaw
            var dn = Object.FindFirstObjectByType<DayNightController>();
            if (dn != null) yield return BrightenToDay(dn, 8f);
            yield return new WaitForSeconds(0.6f);

            // (B) cámara mirando ARRIBA dentro de la carpa + parpadeo (te despertás).
            if (_overhead != null) { Destroy(_overhead.gameObject); _overhead = null; }
            var upCam = MakeUpCam(playerTent, playerYaw);
            var black = MakeBlackOverlay();
            var img = black.GetComponent<RawImage>();
            if (img != null) img.color = Color.black;             // arranca en negro (ojos cerrados)
            yield return FadeOverlay(img, 1f, 0f, 0.6f);          // abrís los ojos
            yield return new WaitForSeconds(0.5f);
            yield return FadeOverlay(img, 0f, 1f, 0.16f);         // parpadeo: cerrás
            yield return FadeOverlay(img, 1f, 0f, 0.35f);         // y abrís
            yield return new WaitForSeconds(0.5f);
            if (black != null) Destroy(black);
            if (upCam != null) Destroy(upCam.gameObject);

            // (C) aparecen AFUERA de la carpa, parados, y control LIBRE (nuevo día).
            Vector3 outside = playerTent + Fwd(playerYaw) * 2.2f;
            if (player != null)
            {
                var pAnim = player.GetComponent<HumanWalkAnim>(); if (pAnim != null) pAnim.seated = false;
                PlaceStandingYaw(player, outside, playerYaw);
                var pcc = player.GetComponent<CharacterController>(); if (pcc != null) pcc.enabled = true;
            }
            if (dog != null) PlaceStandingYaw(dog, outside + Right(playerYaw) * 1.1f, playerYaw);

            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null) party.ForceControl(false);        // volvés a la persona; Rufus te sigue
            PartyController.CinematicLock = false;                // te movés libremente
        }

        // interpola NOCHE -> TARDE ("de día") en 'secs' seg. Al terminar deja la fase Dusk limpia
        // (vuelve la vista larga del atardecer).
        IEnumerator BrightenToDay(DayNightController dn, float secs)
        {
            float t = 0f;
            while (t < secs) { t += Time.deltaTime; dn.SetNightBlend(1f - t / secs); yield return null; }
            dn.SetPhase(DayNightController.Phase.Dusk);
        }

        // cámara dentro de la carpa mirando el TECHO (POV acostado boca arriba).
        Camera MakeUpCam(Vector3 pos, float yaw)
        {
            var go = new GameObject("CampWakeCam");
            Vector3 p = pos; p.y = GroundY(pos, pos.y) + 0.4f;
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(-70f, yaw, 0f);   // pitch negativo = mira arriba
            var cam = go.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.farClipPlane = 300f;
            go.AddComponent<AudioListener>();
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null)
            {
                if (party.personCam != null) party.personCam.gameObject.SetActive(false);
                if (party.dogCam != null)    party.dogCam.gameObject.SetActive(false);
            }
            return cam;
        }

        // overlay negro a pantalla completa (para el parpadeo).
        GameObject MakeBlackOverlay()
        {
            var go = new GameObject("CampWakeBlack");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var img = go.AddComponent<RawImage>();
            img.color = new Color(0f, 0f, 0f, 0f);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }

        // fade del overlay negro de 'from' a 'to' (0 transparente, 1 negro pleno) en 'secs' seg.
        IEnumerator FadeOverlay(RawImage img, float from, float to, float secs)
        {
            float t = 0f;
            while (t < secs) { t += Time.deltaTime; float a = Mathf.Lerp(from, to, t / secs); if (img != null) img.color = new Color(0f, 0f, 0f, a); yield return null; }
            if (img != null) img.color = new Color(0f, 0f, 0f, to);
        }

        // una cacota chiquita (marrón) en el piso.
        GameObject MakePoop(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Cacota";
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            pos.y = GroundY(pos, pos.y) + 0.05f;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.14f, 0.08f, 0.14f);
            var r = go.GetComponent<Renderer>(); if (r != null) r.material.color = new Color(0.28f, 0.18f, 0.08f);
            return go;
        }

        // un personaje se PARA, camina a su carpa y se ACUESTA dentro.
        IEnumerator SleepInTent(Transform who, Vector3 tentPos, float tentYaw, Vector3 offset)
        {
            if (who == null) yield break;
            var anim = who.GetComponent<HumanWalkAnim>(); if (anim != null) anim.seated = false; // se para
            var cc = who.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
            Vector3 dest = tentPos + offset;
            float t = 0f;
            while (Flat2(who.position, dest) > 0.5f && t < 15f) { StepToward(who, dest, 2.0f); t += Time.deltaTime; yield return null; }
            PlaceLyingInTent(who, dest, tentYaw);
        }

        // deja al personaje ACOSTADO (horizontal, boca arriba) en el piso de la carpa.
        void PlaceLyingInTent(Transform who, Vector3 pos, float yaw)
        {
            if (who == null) return;
            var anim = who.GetComponent<HumanWalkAnim>(); if (anim != null) anim.seated = false;
            var cc = who.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false; // queda apagado (acostado)
            Vector3 p = pos; p.y = GroundY(pos, pos.y) + 0.15f;
            who.position = p;
            who.rotation = Quaternion.Euler(-90f, yaw, 0f);   // recostado boca arriba, alineado con la carpa
        }

        // pasa de la cámara de la noche a la del JUGADOR (1ª persona), siguiendo sentado. Habilita el
        // free-look sentado (mirás alrededor con límites, sin darte vuelta ni moverte).
        void SwitchToPlayerCamSeated()
        {
            if (_overhead != null) { Destroy(_overhead.gameObject); _overhead = null; }
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null && party.personCam != null)
            {
                party.personCam.gameObject.SetActive(true);
                if (party.personCam.GetComponent<SeatedLook>() == null)
                    party.personCam.gameObject.AddComponent<SeatedLook>();
            }
        }

        // saca el free-look sentado (el jugador se va a levantar y caminar).
        void RemoveSeatedLook()
        {
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null && party.personCam != null)
            {
                var sl = party.personCam.GetComponent<SeatedLook>();
                if (sl != null) Destroy(sl);
                party.personCam.transform.localRotation = Quaternion.identity;
            }
        }

        // oscurece la escena (sol/ambiente/cielo hacia noche) SIN tocar la niebla ni el clip -> la
        // vista queda ABIERTA durante toda la charla. La fase Night definitiva la pone al ir a dormir.
        IEnumerator DarkenKeepingView(DayNightController dn, float secs)
        {
            float t = 0f;
            while (t < secs)
            {
                float k = t / secs;
                if (dn != null && dn.sun != null)
                {
                    dn.sun.intensity = Mathf.Lerp(0.72f, 0.16f, k);
                    dn.sun.color     = Color.Lerp(new Color(1f, 0.78f, 0.58f), new Color(0.42f, 0.52f, 0.78f), k);
                }
                RenderSettings.ambientLight = Color.Lerp(new Color(0.22f, 0.20f, 0.25f), new Color(0.016f, 0.026f, 0.052f), k);
                if (k >= 0.5f && dn != null && dn.nightSkybox != null && RenderSettings.skybox != dn.nightSkybox)
                    RenderSettings.skybox = dn.nightSkybox;
                t += Time.deltaTime;
                yield return null;
            }
        }

        // cámara fija que mira la fogata mientras se hace de noche (apaga persona/perro).
        void MakeNightCam()
        {
            if (_overhead != null) return;
            var go = new GameObject("CampNightCam");
            go.transform.position = nightCamPos;
            go.transform.rotation = Quaternion.Euler(0f, nightCamYaw, 0f);
            _overhead = go.AddComponent<Camera>();
            _overhead.tag = "MainCamera";
            _overhead.farClipPlane = 500f;
            go.AddComponent<AudioListener>();
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null)
            {
                if (party.personCam != null) party.personCam.gameObject.SetActive(false);
                if (party.dogCam != null)    party.dogCam.gameObject.SetActive(false);
            }
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
        // fantasma de 'tent' colocado en 'pos' mirando 'yaw' (para marcar dónde va antes de armarla).
        GameObject MakeGhostAt(GameObject tent, Vector3 pos, float yaw)
        {
            if (tent == null) return null;
            var ghost = MakeTentGhost(tent);
            if (ghost != null)
            {
                Vector3 gp = pos; gp.y = GroundY(pos, pos.y);
                ghost.transform.position = gp;
                ghost.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            return ghost;
        }

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
            // carpa FANTASMA (transparente) en el lugar, desde el arranque, hasta que la ponga.
            GameObject ghost = MakeGhostAt(tent, greenTentPos, greenTentYaw);

            // se para del lado por donde VIENE (el auto), justo ANTES de la carpa, MIRÁNDOLA. Así
            // frena adelante y no se pasa de largo (antes venía del lado de la fogata = opuesto).
            Vector3 fromCar = car.transform.position - greenTentPos; fromCar.y = 0f;
            fromCar = fromCar.sqrMagnitude < 0.01f ? -Fwd(greenTentYaw) : fromCar.normalized;
            Vector3 standFront = greenTentPos + fromCar * 0.55f; standFront.y = GroundY(standFront, greenTentPos.y);
            float tw = 0f;
            while (green != null && Flat2(green.position, standFront) > 0.25f && tw < 25f) { StepToward(green, standFront, 2.2f); tw += Time.deltaTime; yield return null; }
            FaceTarget(green, greenTentPos);
            yield return new WaitForSeconds(0.5f);   // plantado, mirando el lugar

            // aparece la carpa (el fantasma se reemplaza por la real).
            if (ghost != null) Destroy(ghost);
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
        bool _negroWoodDone;   // el negro ya dejó su leña en la fogata (la fogata se prende cuando esto + la tuya)

        IEnumerator NegroFetchThenSit(Transform negro, Vector3 fire)
        {
            yield return NpcFetchWoodTo(negro, woodPlayerPos, fire, new Vector3(1.3f, 0f, 0.5f));
            _negroWoodDone = true;
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
            // carpa FANTASMA (transparente) en el lugar, desde el arranque, hasta que la pongan.
            GameObject ghost = MakeGhostAt(tent, tentPairPos, tentPairYaw);

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
            FaceTarget(chico, tentPairPos); FaceTarget(chica, tentPairPos);   // MIRAN la carpa (no se dan vuelta todavía)
            yield return new WaitForSeconds(0.5f);   // plantados, mirando -> ahora aparece la carpa

            // aparece la carpa (el fantasma se reemplaza por la real).
            if (ghost != null) Destroy(ghost);
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

            // YA puesta la carpa: la miran un rato y RECIÉN AHÍ se dan vuelta para ir a los troncos.
            yield return new WaitForSeconds(1.2f);
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
            SetCampfireLit(false);   // la fogata arranca APAGADA (se prende al juntar la leña)
        }

        Light _fireLight; ParticleSystem _fireParticles; GameObject _fireEmber;

        // prende/apaga la fogata: la luz, las partículas de fuego y la brasa (glow). Deja el pozo
        // con la leña visible. owner: "la fogata no debe estar prendida hasta que juntemos la leña".
        void SetCampfireLit(bool lit)
        {
            if (_campsite == null) return;
            Transform fire = _campsite.Find("Campfire");
            if (fire == null) return;
            if (_fireLight == null) _fireLight = fire.GetComponentInChildren<Light>(true);
            if (_fireParticles == null) _fireParticles = fire.GetComponentInChildren<ParticleSystem>(true);
            if (_fireEmber == null) { var e = fire.Find("Ember"); if (e != null) _fireEmber = e.gameObject; }

            if (_fireLight != null) _fireLight.enabled = lit;
            if (_fireEmber != null) _fireEmber.SetActive(lit);
            if (_fireParticles != null)
            {
                _fireParticles.gameObject.SetActive(lit);
                if (lit) _fireParticles.Play(); else _fireParticles.Clear();
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
