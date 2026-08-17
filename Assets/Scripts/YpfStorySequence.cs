// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  YpfStorySequence.cs — director de la secuencia de la GASOLINERA (tienda),
//  el guion del owner. Arranca cuando el grupo ya se bajó del auto en la YPF
//  (lo dispara OpeningDriveSequence al terminar la bajada).
//
//  Guion (se construye por ETAPAS, cada una testeable):
//   1. Se dispersan: la CHICA (Friend_FemaleSec) se va al baño; los 2 amigos
//      (MaleCasual, MaleGreenJkt) quedan al lado del auto; vos + Rufus quedan
//      libres para ir a la oficina.  << ESTA ETAPA
//   2. Vos + Rufus golpean la puerta de la oficina; como nadie abre, entran.
//   3. Ven a Richard en la oscuridad en la compu; se acercan, se levanta ->
//      screamer.
//   4. Le piden que cargue nafta; dice que sí y va al auto a cargar.
//   5. Compras: juntar cosas y ponerlas en el mostrador; vuelve a cobrar.
//   6. Preguntan por hielo; los manda afuera "a la vuelta" -> al agarrarlo
//      salen 2 ratas (screamer) y Rufus las persigue (o el jugador-perro).
//   7. Pagan tienda + nafta; grito de la chica; corren al baño y ven salir
//      al viejo verde atrás de ella.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    public class YpfStorySequence : MonoBehaviour
    {
        OpeningDriveSequence op;

        // Ruta de la CHICA hasta el baño: puntos de paso en MUNDO para que rodee los
        // obstáculos (no hay navmesh; camina en línea recta ENTRE puntos, así que hay que
        // elegirlos por un lado despejado). El owner los pasa moviendo el TEST_PLAYER por el
        // camino y leyendo el Position del Inspector.
        Vector3[] chicaWaypoints = new Vector3[]
        {
            new Vector3(463.1929f, 17.07963f, -35.91158f),
            new Vector3(451.1683f, 17.07963f, -37.44011f),
            new Vector3(446.1544f, 17.07963f, -25.04916f),
            new Vector3(439.471f,  17.20835f,  -6.924032f),
        };
        // Punto del UMBRAL (medio del hueco de la puerta abierta) -- el owner lo pasa parando
        // el TEST_PLAYER justo en la abertura. El transform de la puerta cae a un costado del
        // hueco, así que hace falta este punto para que entre DERECHA por la abertura.
        // (0,0,0) = sin setear -> cae al transform de la puerta (aproximado).
        Vector3 chicaDoorway = new Vector3(439.0727f, 17.25314f, -4.907933f);
        // Punto EXACTO adentro del baño donde queda la chica (owner lo dio con el TEST_PLAYER).
        Vector3 chicaBathroomInside = new Vector3(444.2285f, 17.25015f, -2.330376f);

        SwingDoor _openedDoor;
        SwingDoor _officeDoor;   // la puerta de la oficina (trabada desde el inicio; golpeable en Etapa 2)
        SwingDoor _traseraDoor;  // la puerta trasera (owner: también trabada hasta el golpeo)
        bool _friendPassedStoreDoor;   // el amigo ya cruzó la puerta de la tienda -> habilita el golpeo

        // ── COMPRA: tras el susto, barrera invisible en la puerta de la tienda hasta juntar los
        //    objetos de las estanterías (owner). Rellená shelfItemPositions con las coords del owner.
        GameObject _exitBarrier;
        GameObject _counterMarker;   // indicador amarillo en el mostrador (dónde dejar las cosas)
        [Header("Compra (barrera + objetos de estantería)")]
        public Vector3[] shelfItemPositions = new Vector3[]     // coords de los objetos (owner)
        {
            new Vector3(455.7492f, 19.30439f, -16.88786f),
            new Vector3(459.6759f, 18.82218f, -11.1888f),
            new Vector3(456.6622f, 18.82218f,  -9.017525f),
        };
        public float itemPickupRadius = 1.6f;                   // distancia para agarrar/dejar con E
        public Vector3 exitBarrierSize = new Vector3(1.8f, 3f, 1.8f); // tamaño de la barrera invisible
        public Vector3 counterPos = new Vector3(465.401f, 18.58023f, -16.04165f); // MOSTRADOR (owner)
        public float counterRadius = 2.0f;                      // distancia al mostrador para dejar
        bool _richardPassedStore;                               // los objetos aparecen recién cuando pasa
        bool _shoppingDone;                                     // se dejó todo en el mostrador -> arranca Etapa 5

        // ── ETAPA 5: hielos + ratas + playero "disfrazado" (owner) ──────────
        [Header("Etapa 5 (hielos / ratas)")]
        public Vector3 behindCounterPos = new Vector3(466.6333f, 17.25815f, -15.84829f); // Richard detrás del mostrador (owner)
        public Vector3 freezerPos  = new Vector3(469.3697f, 19.39098f, -15.73312f); // heladera de hielo (owner)
        public float freezerRange  = 3.0f;                      // al acercarte tanto, salen las ratas
        public int   ratCount      = 3;
        public float ratFleeSpeed  = 2.7f;                      // velocidad al huir del perro
        public float ratWanderSpeed = 1.9f;                     // velocidad corriendo random por el área
        // owner: Rufus atrapa APUNTÁNDOLAS (crosshair encima) + E, no poniéndose arriba.
        public float ratCatchRange = 4f;                        // distancia máxima para atrapar apuntando
        public float ratAimAngle   = 16f;                       // tolerancia de mira (°) sobre la rata
        public float ratEscapeHold = 2.5f;                      // seg en el borde lejos del perro = se escapa
        public int   richardDoorWaitCount = 3;                  // waypoints que llevan a ESPERAR detrás de la trasera
        // owner: las ratas SALEN de la heladera y corren aleatoriamente por este rectángulo del
        // parking (calculado de las 4 esquinas que dio el owner). Área = centro ± mitad.
        public Vector3 ratAreaCenter   = new Vector3(480.51f, 17.1f, -10.16f);
        public Vector3 ratAreaHalfSize = new Vector3(9.67f, 0f, 11.5f);
        // owner: al atrapar las ratas, Richard abre la TRASERA y sale a la heladera POR FUERA.
        // Ruta: detrás del mostrador -> puerta trasera -> afuera -> heladera. (coords del owner)
        public Vector3[] richardToFreezerWaypoints = new Vector3[]
        {
            new Vector3(467.4767f, 17.25815f, -10.7598f),
            new Vector3(465.1414f, 17.25815f, -10.26163f),
            new Vector3(466.2047f, 17.25814f,  -0.8754411f), // hacia la puerta trasera
            new Vector3(471.7304f, 17.07963f,  -0.9284623f), // cruza la trasera (afuera)
            new Vector3(470.8411f, 17.23242f, -14.38454f),   // afuera, al lado de la heladera
        };

        // ── ETAPA 6: pagar + grito del baño (owner) ─────────────────────────
        [Header("Etapa 6 (pagar / grito baño)")]
        public Vector3 bathroomGatherPos = new Vector3(442.7902f, 17.07963f, -12.41807f); // afuera del baño (owner)
        public float payRange = 2.5f;                      // distancia al mostrador para pagar (E)
        // owner: al salir del baño, la CHICA va hasta acá (sale por la puerta y se junta con el grupo).
        public Vector3[] chicaExitWaypoints = new Vector3[]
        {
            new Vector3(439.1003f, 17.25015f, -2.251966f), // se alinea con la puerta (no atraviesa la pared)
            new Vector3(439.1226f, 17.22934f, -6.878397f), // puerta del baño
            new Vector3(442.8139f, 17.07963f, -11.55511f), // afuera, con el grupo
        };
        // owner: la chica espera detrás de la puerta; cuando los jugadores pasan ESTE punto, sale
        // del baño y 1 s después sale el VIEJO VERDE (arranca en viejoStartPos y hace viejoWaypoints).
        public Vector3 chicaTriggerPos = new Vector3(446.857f, 17.07963f, -21.45106f);
        public float chicaTriggerRange = 3f;
        public Vector3 viejoStartPos = new Vector3(447.5974f, 17.25015f, -2.219909f);
        public Vector3[] viejoWaypoints = new Vector3[]   // salida del baño (por la puerta)
        {
            new Vector3(439.1076f, 17.25015f, -2.202659f),
            new Vector3(439.2001f, 17.25314f, -6.059659f),
            new Vector3(439.1745f, 17.07963f, -8.331011f),
        };
        public Vector3 viejoStandPos = new Vector3(438.8383f, 17.07963f, -13.16724f);  // se para acá y chifla
        public Vector3 casualConfrontPos = new Vector3(442.0002f, 17.07963f, -13.39239f); // MaleCasual enfrente
        public Vector3[] viejoToCarWaypoints = new Vector3[]  // se va al auto rojo (owner)
        {
            new Vector3(439.0986f, 17.07963f, -19.80025f),
            new Vector3(443.2964f, 17.07963f, -35.51123f),
            new Vector3(448.0143f, 17.07963f, -50.78497f),
            new Vector3(450.1569f, 17.07963f, -57.93616f),
            new Vector3(458.0691f, 17.07963f, -56.76144f), // puerta del auto rojo -> acá desaparece
        };

        // ── ETAPA 7: se suben al auto y manejan al campamento (owner) ────────
        // Primero los 3 amigos van JUNTOS por el camino común, y después cada uno a SU puerta.
        [Header("Etapa 7 (subir al auto)")]
        public Vector3[] friendsCommonPath = new Vector3[]
        {
            new Vector3(443.6569f, 17.07963f, -24.35711f),
            new Vector3(446.7239f, 17.07963f, -37.77824f),
            new Vector3(456.132f,  17.07963f, -39.42498f),
        };
        // owner: son 2 PUERTAS (dos amigos van por la misma). Izquierda: MaleCasual + el negro.
        public Vector3[] leftDoorTail = new Vector3[]     // puerta trasera izquierda
        {
            new Vector3(459.7257f, 17.07963f, -44.16063f),
            new Vector3(464.9204f, 17.07963f, -42.68484f),
            new Vector3(464.4377f, 17.07963f, -41.0427f),
        };
        public Vector3[] rightDoorTail = new Vector3[]    // puerta trasera derecha (la chica)
        {
            new Vector3(458.0729f, 17.07963f, -36.76767f),
            new Vector3(464.5009f, 17.07963f, -35.70797f),
            new Vector3(464.2852f, 17.07963f, -37.35142f),
        };
        // owner: RUTAS que corren los 2 amigos hasta afuera del baño (para no atravesar cosas).
        // Cada uno arranca de donde está (MaleCasual en la tienda, MaleGreenJkt en el auto).
        public Vector3[] casualToBathWaypoints = new Vector3[]   // el amigo de la TIENDA (owner)
        {
            new Vector3(462.687f,  17.25814f, -19.75606f), // vuelve a la puerta de la tienda
            new Vector3(462.9134f, 17.07963f, -22.03736f),
            new Vector3(446.6402f, 17.07963f, -22.11976f),
            new Vector3(444.5929f, 17.07963f, -16.86514f),
            new Vector3(444.9358f, 17.07963f, -12.86991f), // afuera del baño, AL LADO DEL NEGRO (owner)
        };
        public Vector3[] greenJktToBathWaypoints = new Vector3[]  // el negro, desde el auto (owner)
        {
            new Vector3(464.2935f, 17.46277f, -34.61959f),
            new Vector3(464.0334f, 17.07963f, -30.75196f),
            new Vector3(449.0485f, 17.07963f, -23.24833f),
            new Vector3(445.5075f, 17.07963f, -18.70573f),
            new Vector3(443.8815f, 17.07963f, -13.62787f), // afuera del baño
        };
        // owner: puerta TRASERA a trabar (solo la abre el playero). Posición del owner.
        public Vector3 trasereDoorPos = new Vector3(470.7033f, 17.25814f, -0.901195f);

        // Traba la puerta de la oficina (y la trasera si el owner la dio) desde el inicio: no se
        // pueden abrir hasta que la Etapa 2 las pase a modo "golpear".
        void LockOfficeDoorsUpfront()
        {
            Vector3 refPos = officeKnockPos != Vector3.zero ? officeKnockPos
                           : (GameObject.Find("Playero_Richard") is GameObject r ? r.transform.position : chicaBathroomInside);
            _officeDoor = FindOfficeDoor(refPos);
            if (_officeDoor != null)
            {
                _officeDoor.locked = true; _officeDoor.knockToUnlock = false;
                // owner: "que abra del lado del picaporte, para el otro sentido" -> bisagra en el
                // otro borde + sentido invertido (combinación que faltaba probar).
                _officeDoor.SetHinge(true);
                _officeDoor.openAngle = -Mathf.Abs(_officeDoor.openAngle);
            }

            if (trasereDoorPos != Vector3.zero)
            {
                _traseraDoor = NearestDoorTo(trasereDoorPos);
                if (_traseraDoor != null) { _traseraDoor.locked = true; _traseraDoor.knockToUnlock = false; }
            }
        }

        // owner: Richard un poquito más alto y con colisión. Se ajusta en runtime (los valores
        // están horneados en Generate y no queremos regenerar). El collider escala con él.
        void SetupRichard()
        {
            var r = GameObject.Find("Playero_Richard");
            if (r == null) return;
            r.transform.localScale *= 1.08f;   // un poquito más alto
            var col = r.GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = r.AddComponent<CapsuleCollider>();
                col.height = 1.7f; col.radius = 0.3f; col.center = new Vector3(0f, 0.85f, 0f);
            }
            col.isTrigger = false;   // que el jugador choque de verdad
        }

        SwingDoor _bathDoor;   // puerta del baño (trabada para el jugador hasta que el viejo se va)

        // Pone al viejo verde ADENTRO del baño (quieto) y traba el baño para el jugador. La chica
        // igual lo abre por código (SetOpen ignora locked). Se destraba cuando el viejo se va.
        void SetupViejoAndBathroom()
        {
            var viejoGo = GameObject.Find("CreepyOldMan");
            if (viejoGo != null)
            {
                var fw = viejoGo.GetComponent<FriendWander>(); if (fw != null) fw.enabled = false;
                Teleport(viejoGo.transform, viejoStartPos);
            }
            _bathDoor = FindEntranceDoor();
            if (_bathDoor != null) { _bathDoor.locked = true; _bathDoor.knockToUnlock = false; }
        }

        // la SwingDoor más cercana a un punto (para la trasera).
        SwingDoor NearestDoorTo(Vector3 p)
        {
            SwingDoor best = null; float bd = float.MaxValue;
            foreach (var d in Object.FindObjectsByType<SwingDoor>(FindObjectsSortMode.None))
            {
                float dist = Vector3.Distance(DoorCenter(d), p);
                if (dist < bd) { bd = dist; best = d; }
            }
            return best;
        }
        Vector3 _doorwayPoint;   // centro del hueco de la puerta (para entrar derechita por ahí)
        bool _haveDoorway;

        // lo llama OpeningDriveSequence cuando el grupo ya bajó en la YPF.
        public void Begin(OpeningDriveSequence seq)
        {
            op = seq;
            // owner: "que salga donde te dije, que no se teepee primero delante del auto".
            // Se la teletransporta al primer punto YA, en el MISMO frame que la bajó (antes de
            // que se dibuje), así nunca se ve un instante al lado del auto.
            var chica = op != null ? op.friendFemaleSec : null;
            if (chica != null && chicaWaypoints != null && chicaWaypoints.Length > 0)
                Teleport(chica, chicaWaypoints[0]);
            // el amigo (MaleCasual) también aparece directo en su spawn (wp1), sin flash.
            var storeFriend = op != null ? op.friendMaleCasual : null;
            if (storeFriend != null && friendStoreWaypoints != null && friendStoreWaypoints.Length > 0)
                Teleport(storeFriend, friendStoreWaypoints[0]);
            // el otro amigo (MaleGreenJkt) aparece en su spawn al lado del auto.
            var greenJkt = op != null ? op.friendMaleGreenJkt : null;
            if (greenJkt != null) Teleport(greenJkt, greenJktSpawn);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // pequeño respiro tras la bajada (que terminen las corrutinas de ExitRoutine).
            yield return new WaitForSeconds(0.6f);

            // owner: "que no se pueda abrir la puerta de la oficina ni la trasera hasta que se
            // habilite el golpeo". Se traban desde YA (apenas bajan del auto).
            LockOfficeDoorsUpfront();

            // owner: el viejo verde arranca ADENTRO del baño (no afuera) y quieto; el baño queda
            // TRABADO para el jugador (solo la chica lo abre, por código) hasta que el viejo se va.
            SetupViejoAndBathroom();

            // owner: Richard un poquito más alto + colisión (ajustado en runtime, sin regenerar).
            SetupRichard();

            int cp = OpeningDriveSequence.ReadCheckpoint();
            if (cp >= 2)
            {
                // DEBUG (checkpoint Tienda): dispersión INSTANTÁNEA -- chica ya en el baño,
                // amigos ya ubicados, tienda abierta -> directo a golpear la oficina.
                var chica = op != null ? op.friendFemaleSec : null;
                if (chica != null) Teleport(chica, chicaBathroomInside);
                var storeFriend = op != null ? op.friendMaleCasual : null;
                if (storeFriend != null && friendStoreWaypoints != null && friendStoreWaypoints.Length > 0)
                    Teleport(storeFriend, friendStoreWaypoints[friendStoreWaypoints.Length - 1]);
                var greenJkt = op != null ? op.friendMaleGreenJkt : null;
                if (greenJkt != null) Teleport(greenJkt, greenJktStand);
                var store = FindStoreDoor();
                if (store != null) { store.locked = false; store.SetOpen(true); }
            }
            else
            {
                // owner: "tienen que ir los dos a la vez" -- chica al baño y amigo a la tienda EN
                // PARALELO (siguen corriendo en el fondo).
                StartCoroutine(Stage1_Disperse());
                StartCoroutine(Stage1b_FriendEntersStore());
                StartCoroutine(Stage1c_GreenJktStaysByCar());   // el otro amigo queda al lado del auto
                // owner: el golpeo se habilita cuando el AMIGO PASA la puerta de la tienda (no
                // cuando la chica llega al baño). No esperamos a que terminen todas.
                yield return new WaitUntil(() => _friendPassedStoreDoor);
            }

            yield return Stage2_KnockOffice();
            yield return Stage3_RichardScreamer();
            yield return Stage4_Gas();
            yield return Stage5_Ice();
            yield return Stage6_PayScream();
            yield return Stage7_Board();

            // (próximas etapas van acá, en orden)
        }

        // ── ETAPA 4: le piden nafta, Richard dice que sí y va al auto a cargar ──
        // Después del screamer: diálogo corto, y Richard camina hasta el auto (a cargar nafta).
        // Mientras tanto quedan libres para ir a comprar (Etapa 5 = tienda + mostrador).
        // Ruta de Richard de la oficina al surtidor/rociador (owner). El último punto = el rociador.
        Vector3[] richardGasWaypoints = new Vector3[]
        {
            new Vector3(465.9806f, 17.25814f,  -1.808672f),
            new Vector3(466.026f,  17.25942f,  -4.969123f),
            new Vector3(462.5287f, 17.25815f, -11.8829f),
            new Vector3(462.9436f, 17.25815f, -19.30591f),
            new Vector3(463.0858f, 17.07963f, -31.51123f),
            new Vector3(463.1381f, 17.46277f, -32.08751f), // rociador
        };
        IEnumerator Stage4_Gas()
        {
            var richardGo = GameObject.Find("Playero_Richard");
            Transform richard = richardGo != null ? richardGo.transform : null;
            if (richard == null) yield break;

            // owner: TRAS EL SUSTO -> barrera invisible SIEMPRE en la puerta de la tienda. Bloquea
            // al jugador aunque Richard ABRA la puerta para pasar (la barrera es un collider aparte).
            // Además trabo la E de tienda/trasera (la OFICINA no se toca). Si hay objetos de
            // estantería, la barrera baja al juntarlos; si no, baja cuando Richard vuelve.
            RaiseExitBarrier();
            LockPlayerIn(true);
            bool hasShopping = shelfItemPositions != null && shelfItemPositions.Length > 0;
            if (hasShopping) StartCoroutine(ShoppingLoop());

            // diálogo (cada línea = un Say; agregar/editar es trivial, ver helper Say abajo).
            // owner: "habrá más diálogos entre medio, tal vez luego los modifico".
            yield return Say("Vos: ¿Nos podés cargar nafta?", 2f);
            yield return Say("Richard: Dale... ahí voy.", 2f);
            yield return Say("Amigo: Che, vamos comprando un par de cosas.", 2.2f);

            // animación de patas al caminar
            var anim = richard.gameObject.AddComponent<HumanWalkAnim>();
            anim.armSpread = 0.32f;   // owner: brazos un poquito más separados del cuerpo
            anim.limbs = new[]
            {
                new HumanWalkAnim.Limb { bone = "thigh.L",    phase =  1f },
                new HumanWalkAnim.Limb { bone = "thigh.R",    phase = -1f },
                new HumanWalkAnim.Limb { bone = "shoulder.L", phase = -1f },
                new HumanWalkAnim.Limb { bone = "shoulder.R", phase =  1f },
            };

            // abrir la puerta de la oficina para que salga (owner: la de la OFICINA NO se cierra)
            if (_officeDoor != null) _officeDoor.SetOpen(true);

            // owner: Richard NO tiene que atravesar la puerta de la tienda -> se abre cuando llega
            // y se cierra cuando pasa (su ruta cruza justo por ahí). SetOpen es por código, así que
            // funciona aunque la puerta esté trabada para el jugador (la barrera lo sigue frenando).
            var storeDoorPass = FindStoreDoor();
            if (storeDoorPass != null)
                StartCoroutine(RichardThroughStoreDoor(richard, storeDoorPass));

            // Richard camina la RUTA del owner (oficina -> surtidor/rociador).
            if (richardGasWaypoints != null)
                foreach (var wp in richardGasWaypoints)
                    yield return WalkTo(richard, wp, speed: 1.6f, stopDist: 0.35f);
            // asentar EXACTO en el rociador (último punto, Y a mano)
            if (richardGasWaypoints != null && richardGasWaypoints.Length > 0)
                Teleport(richard, richardGasWaypoints[richardGasWaypoints.Length - 1]);

            // Richard "carga" la nafta. El jugador está ENCERRADO (no ve la carga, owner), así que
            // no hace falta pistola ni manguera: solo esperamos un rato mientras "carga".
            yield return Say("Richard: Ya te cargo...", 2f);
            yield return new WaitForSeconds(3.5f);

            // vuelve a ATRÁS DEL AUTO (ruta del owner).
            if (richardBackWaypoints != null)
                foreach (var wp in richardBackWaypoints)
                    yield return WalkTo(richard, wp, speed: 1.5f, stopDist: 0.3f);
            if (richardBackWaypoints != null && richardBackWaypoints.Length > 0)
            {
                Teleport(richard, richardBackWaypoints[richardBackWaypoints.Length - 1]);
                var car = Object.FindFirstObjectByType<CarController>();
                if (car != null)
                {
                    Vector3 look = car.transform.position - richard.position; look.y = 0f;
                    if (look.sqrMagnitude > 1e-4f) richard.rotation = Quaternion.LookRotation(look.normalized);
                }
            }

            // Richard volvió con la nafta. Si NO hay compra configurada, la barrera baja acá
            // (fallback). Si hay compra, la barrera baja sola al juntar los objetos (en ShoppingLoop).
            if (!hasShopping) { DropExitBarrier(); LockPlayerIn(false); }
        }

        // ── BARRERA + COMPRA ─────────────────────────────────────────────────
        // Barrera invisible en la puerta de la tienda: un BoxCollider sólido (sin malla) que frena
        // al jugador. Se levanta tras el susto y baja al juntar todos los objetos de estantería.
        void RaiseExitBarrier()
        {
            if (_exitBarrier != null) return;
            _exitBarrier = new GameObject("ExitBarrier");
            _exitBarrier.transform.position = new Vector3(storeDoorPos.x,
                                                          storeDoorPos.y + exitBarrierSize.y * 0.5f,
                                                          storeDoorPos.z);
            var bc = _exitBarrier.AddComponent<BoxCollider>();
            bc.size = exitBarrierSize;   // sólido (no trigger) -> bloquea al jugador
            Debug.Log($"<color=yellow>[YPF] barrera de salida ARRIBA en {storeDoorPos}</color>");
        }

        void DropExitBarrier()
        {
            if (_exitBarrier != null) Destroy(_exitBarrier);
            _exitBarrier = null;
            Debug.Log("<color=yellow>[YPF] barrera de salida ABAJO (compra terminada)</color>");
        }

        // Instancia los objetos coleccionables en las estanterías, espera a que el jugador (o el
        // perro) los junte caminando cerca, y cuando están todos, baja la barrera.
        IEnumerator ShoppingLoop()
        {
            // owner: los objetos aparecen RECIÉN cuando el playero pasó la puerta de la tienda.
            yield return new WaitUntil(() => _richardPassedStore);

            var items = new System.Collections.Generic.List<GameObject>();
            foreach (var p in shelfItemPositions) items.Add(CreateStoreItem(p));
            int total = items.Count;
            int deposited = 0;
            GameObject carried = null;
            _counterMarker = CreateCounterMarker(counterPos);   // indicador amarillo (dónde dejar)
            yield return Say($"Agarrá los objetos con E y llevalos al mostrador (0/{total}).", 2.5f);

            // owner: se agarran con E uno por uno y se llevan al mostrador (counterPos).
            while (deposited < total)
            {
                Transform cam = ActiveCam();   // cámara activa (persona o perro)

                // el objeto agarrado sigue a la cámara (lo llevás en la mano)
                if (carried != null && cam != null)
                    carried.transform.position = cam.position + cam.forward * 0.6f - cam.up * 0.15f;

                var kb = Keyboard.current;
                if (kb != null && kb.eKey.wasPressedThisFrame && !SettingsMenu.IsOpen && cam != null)
                {
                    if (carried == null)
                    {
                        // agarrar el objeto más cercano en rango (mirando/parado cerca)
                        GameObject best = NearestItem(items, cam.position, itemPickupRadius);
                        if (best != null)
                        {
                            carried = best;
                            _hint = "Llevalo al mostrador (E para dejarlo)";
                        }
                    }
                    else if (counterPos != Vector3.zero &&
                             Vector3.Distance(Flat(cam.position), Flat(counterPos)) <= counterRadius)
                    {
                        // dejar en el mostrador
                        items.Remove(carried);
                        carried.transform.position = counterPos + Vector3.up * (0.9f + deposited * 0.12f);
                        carried = null; deposited++;
                        _hint = "";
                        StartCoroutine(Say($"Dejaste un objeto en el mostrador ({deposited}/{total}).", 1.6f));
                    }
                }
                yield return null;
            }

            if (_counterMarker != null) Destroy(_counterMarker);
            yield return Say("Ya dejaste todo en el mostrador.", 2f);
            DropExitBarrier();
            LockPlayerIn(false);
            _shoppingDone = true;   // habilita la Etapa 5 (hielos)
        }

        // indicador amarillo en el mostrador (dónde dejar las cosas). Disco chato brillante + flota.
        GameObject CreateCounterMarker(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "CounterMarker";
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.position = at;
            go.transform.localScale = new Vector3(0.55f, 0.02f, 0.55f); // disco chato
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(1f, 0.9f, 0.15f);
                if (r.material.HasProperty("_EmissionColor"))
                {
                    r.material.EnableKeyword("_EMISSION");
                    r.material.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.1f) * 1.6f);
                }
            }
            return go;
        }

        // objeto (de los que quedan en estantería) más cercano a 'from' dentro de 'range' (horizontal).
        static GameObject NearestItem(System.Collections.Generic.List<GameObject> items, Vector3 from, float range)
        {
            GameObject best = null; float bd = range;
            foreach (var it in items)
            {
                if (it == null) continue;
                float d = Vector3.Distance(Flat(it.transform.position), Flat(from));
                if (d <= bd) { bd = d; best = it; }
            }
            return best;
        }

        // cámara activa = la del AudioListener prendido (persona o perro).
        Transform ActiveCam()
        {
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.isActiveAndEnabled) return l.transform;
            return Camera.main != null ? Camera.main.transform : null;
        }

        // ── ETAPA 5: hielos + ratas + playero "disfrazado" (owner) ───────────
        // Cuando dejaste todo en el mostrador: Richard vuelve y se para DETRÁS del mostrador;
        // le preguntás por los hielos y dice "atrás". Vas a la heladera y al acercarte salen 3
        // ratas que Rufus persigue y atrapa con E. Atrapadas, sale "el otro playero" (el mismo
        // Richard con otra gorra y otra voz) y les da los hielos.
        IEnumerator Stage5_Ice()
        {
            yield return new WaitUntil(() => _shoppingDone);

            var richardGo = GameObject.Find("Playero_Richard");
            Transform richard = richardGo != null ? richardGo.transform : null;

            // owner: Richard VUELVE CAMINANDO (no teletransporte) hasta detrás del mostrador.
            if (richard != null)
            {
                // re-abrir/cerrar la puerta de la tienda al re-entrar
                var storeDoorRet = FindStoreDoor();
                if (storeDoorRet != null) StartCoroutine(RichardThroughStoreDoor(richard, storeDoorRet));

                foreach (var wp in richardReturnWaypoints)
                    yield return WalkTo(richard, wp, speed: 1.6f, stopDist: 0.35f);
                if (behindCounterPos != Vector3.zero) Teleport(richard, behindCounterPos);
                FaceTowards(richard, counterPos);
            }

            Debug.Log("<color=yellow>[YPF] Etapa 5 START (compra lista) -> Richard atrás del mostrador</color>");
            yield return Say("Vos: ¿Y los hielos?", 2f);
            yield return Say("Richard: Atrás... ahí te atiende el otro.", 2.4f);

            // el jugador (o el perro) va hasta la heladera de hielo. Log de distancia cada ~1s.
            _hint = "Andá a la heladera de hielo (atrás)";
            Debug.Log($"<color=yellow>[YPF] andá a la heladera en {freezerPos} (hace falta acercarte a <= {freezerRange} m)</color>");
            float logT = 0f;
            while (true)
            {
                Transform c  = ActiveCam();
                Transform pl = (op != null && op.player != null) ? op.player.transform : null;
                Transform dg = (op != null && op.dog != null)    ? op.dog.transform    : null;
                float best = float.MaxValue;
                if (c  != null) best = Mathf.Min(best, Vector3.Distance(Flat(c.position),  Flat(freezerPos)));
                if (pl != null) best = Mathf.Min(best, Vector3.Distance(Flat(pl.position), Flat(freezerPos)));
                if (dg != null) best = Mathf.Min(best, Vector3.Distance(Flat(dg.position), Flat(freezerPos)));
                if (best <= freezerRange) break;
                logT += Time.deltaTime;
                if (logT >= 1f) { Debug.Log($"<color=yellow>[YPF] distancia a la heladera = {best:F1} m</color>"); logT = 0f; }
                yield return null;
            }
            _hint = "";

            // al acercarte a sacar hielo: SALEN 3 RATAS de la heladera y corren ALEATORIAMENTE por
            // el área del parking; Rufus las persigue y atrapa con E.
            yield return Say("¡Ratas! Rufus, ¡atrapalas! (E cerca de cada una)", 2.4f);
            var rats = new System.Collections.Generic.List<GameObject>();
            var ratTargets = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < ratCount; i++)
            {
                Vector3 sp = freezerPos + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
                sp.y = GroundYAt(sp);
                rats.Add(CreateRat(sp));
                ratTargets.Add(RandomRatPoint());   // primer destino random dentro del área
            }
            Debug.Log($"<color=lime>[YPF] SPAWNEADAS {rats.Count} ratas en la heladera {freezerPos} (piso Y≈{GroundYAt(freezerPos):F2})</color>");

            // owner: apenas SALEN las ratas, Richard va a ESPERAR detrás de la trasera (todavía no
            // sale). Recién sale cuando se resuelven todas (atrapadas o escapadas).
            bool richardAtDoor = false;
            if (richard != null)
                StartCoroutine(RichardGoWaitBehindDoor(richard, () => richardAtDoor = true));

            var ratEscape = new System.Collections.Generic.List<float>();
            for (int i = 0; i < rats.Count; i++) ratEscape.Add(0f);

            float encounterT = 0f;   // gracia: sin escapes al principio (spawnean en el borde)
            while (rats.Count > 0)   // hasta que no quede ninguna (atrapada o escapada)
            {
                encounterT += Time.deltaTime;
                Transform dog = (op != null && op.dog != null) ? op.dog.transform : null;
                Transform cam = ActiveCam();
                bool camIsDog = cam != null && op != null && op.dog != null && cam.IsChildOf(op.dog.transform);
                float dt = Time.deltaTime;
                var kb = Keyboard.current;
                bool ePressed = kb != null && kb.eKey.wasPressedThisFrame && !SettingsMenu.IsOpen;
                bool ratAimed = false;   // alguna rata bajo la mira -> "[E] Atrapar"

                for (int i = rats.Count - 1; i >= 0; i--)
                {
                    if (rats[i] == null) { rats.RemoveAt(i); ratTargets.RemoveAt(i); ratEscape.RemoveAt(i); continue; }
                    var rt = rats[i].transform;
                    Vector3 pos = rt.position;

                    // dirección: si el perro está cerca, HUIR; si no, correr hacia su destino random.
                    Vector3 dir; float speed;
                    Vector3 away = dog != null ? pos - dog.position : Vector3.zero; away.y = 0f;
                    float dogDist = dog != null ? away.magnitude : 999f;
                    if (dog != null && dogDist < 3.5f && away.sqrMagnitude > 1e-4f)
                    {
                        dir = away.normalized; speed = ratFleeSpeed;
                    }
                    else
                    {
                        Vector3 to = ratTargets[i] - pos; to.y = 0f;
                        if (to.magnitude < 0.5f) { ratTargets[i] = RandomRatPoint(); to = ratTargets[i] - pos; to.y = 0f; }
                        dir = to.sqrMagnitude > 1e-4f ? to.normalized : rt.forward;
                        speed = ratWanderSpeed;
                    }

                    Vector3 np = pos + dir * speed * dt;
                    float minX = ratAreaCenter.x - ratAreaHalfSize.x, maxX = ratAreaCenter.x + ratAreaHalfSize.x;
                    float minZ = ratAreaCenter.z - ratAreaHalfSize.z, maxZ = ratAreaCenter.z + ratAreaHalfSize.z;
                    np.x = Mathf.Clamp(np.x, minX, maxX);
                    np.z = Mathf.Clamp(np.z, minZ, maxZ);
                    np.y = GroundYAt(np);
                    rt.position = np;
                    if (dir.sqrMagnitude > 1e-4f) rt.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));

                    // ESCAPE: pasada la gracia, si llegó al borde y el perro está lejos un rato -> se escapa.
                    bool atEdge = np.x <= minX + 0.1f || np.x >= maxX - 0.1f || np.z <= minZ + 0.1f || np.z >= maxZ - 0.1f;
                    if (encounterT > 8f && atEdge && dogDist > 5f) ratEscape[i] += dt; else ratEscape[i] = 0f;
                    if (ratEscape[i] >= ratEscapeHold)
                    {
                        Destroy(rats[i]); rats.RemoveAt(i); ratTargets.RemoveAt(i); ratEscape.RemoveAt(i);
                        StartCoroutine(Say("¡Una rata se escapó!", 1.3f));
                        continue;
                    }

                    // ATRAPAR APUNTANDO: la mira del perro sobre la rata + E (no por estar encima).
                    bool aiming = false;
                    if (camIsDog)
                    {
                        Vector3 toRat = rt.position - cam.position;
                        if (toRat.magnitude <= ratCatchRange && Vector3.Angle(cam.forward, toRat) <= ratAimAngle)
                            aiming = true;
                    }
                    if (aiming) ratAimed = true;
                    if (aiming && ePressed)
                    {
                        Destroy(rats[i]); rats.RemoveAt(i); ratTargets.RemoveAt(i); ratEscape.RemoveAt(i);
                        StartCoroutine(Say("¡Atrapaste una rata!", 1.3f));
                        ePressed = false;
                    }
                }
                _hint = ratAimed ? "[ E ]  Atrapar la rata" : "";
                yield return null;
            }
            _hint = "";

            // ratas resueltas -> Richard (que esperaba detrás) ABRE LA TRASERA y sale a la heladera
            // POR FUERA; ahí se pone la gorra y hace de "el otro playero".
            yield return Say("...", 0.8f);
            if (richard != null)
            {
                yield return new WaitUntil(() => richardAtDoor);   // asegurar que ya está detrás de la puerta

                var trasera = _traseraDoor != null ? _traseraDoor : NearestDoorTo(trasereDoorPos);
                if (trasera != null) trasera.SetOpen(true);

                // camina el TRAMO DE SALIDA (desde detrás de la puerta hasta la heladera)
                int start = Mathf.Clamp(richardDoorWaitCount, 0, richardToFreezerWaypoints.Length);
                if (richardToFreezerWaypoints.Length > start)
                {
                    for (int i = start; i < richardToFreezerWaypoints.Length; i++)
                        yield return WalkTo(richard, richardToFreezerWaypoints[i], speed: 1.6f, stopDist: 0.35f);
                    if (trasera != null) StartCoroutine(CloseWhenClear(richard, trasereDoorPos, trasera, 2.5f));
                }
                else
                {
                    Vector3 spot = freezerPos + new Vector3(-1.3f, 0f, 0f);
                    float y = behindCounterPos != Vector3.zero ? behindCounterPos.y : richard.position.y;
                    Teleport(richard, new Vector3(spot.x, y, spot.z));
                }
                FaceTowards(richard, ActiveCam() != null ? ActiveCam().position : counterPos);
                AddCap(richard);   // "otra gorra"
            }
            yield return Say("Otro playero (?): Uh, disculpen las ratas, muchachos.", 2.6f);
            yield return Say("Otro playero (?): Acá tienen los hielos. Agarralos.", 2.2f);

            // owner: el playero se QUEDA parado dándote los hielos; los agarrás tocando E APUNTÁNDOLO
            // (el cartel aparece solo cuando lo estás mirando de cerca, no siempre).
            if (richard != null)
            {
                bool tookIce = false;
                while (!tookIce)
                {
                    Transform cam = ActiveCam();
                    bool aiming = false;
                    if (cam != null)
                    {
                        Vector3 to = (richard.position + Vector3.up * 1.0f) - cam.position;
                        if (to.magnitude <= 3f && Vector3.Angle(cam.forward, to) <= 22f) aiming = true;
                    }
                    _hint = aiming ? "Agarrá los hielos (E)" : "";
                    var kb = Keyboard.current;
                    if (aiming && kb != null && kb.eKey.wasPressedThisFrame && !SettingsMenu.IsOpen) tookIce = true;
                    yield return null;
                }
                _hint = "";
                yield return Say("Vos: Gracias. Buenísimo.", 1.6f);
            }
        }

        // Richard camina hasta ESPERAR detrás de la trasera (primeros waypoints), sin salir.
        IEnumerator RichardGoWaitBehindDoor(Transform richard, System.Action onArrived)
        {
            int n = Mathf.Min(richardDoorWaitCount, richardToFreezerWaypoints.Length);
            for (int i = 0; i < n; i++)
                yield return WalkTo(richard, richardToFreezerWaypoints[i], speed: 1.6f, stopDist: 0.35f);
            onArrived?.Invoke();
        }

        // ── ETAPA 6: pagar + grito del baño ──────────────────────────────────
        // Richard da los hielos y vuelve por donde vino hasta el mostrador. El jugador va a pagar
        // y, al hacerlo, se escucha un GRITO del baño (la chica: el viejo verde le quiso tocar el
        // culo). Todos corren hasta afuera del baño.
        IEnumerator Stage6_PayScream()
        {
            var richardGo = GameObject.Find("Playero_Richard");
            Transform richard = richardGo != null ? richardGo.transform : null;

            // Richard VUELVE por donde vino (reversa de la ruta a la heladera) hasta el mostrador.
            if (richard != null)
            {
                var trasera = _traseraDoor != null ? _traseraDoor : NearestDoorTo(trasereDoorPos);
                // owner: la trasera se abre cuando Richard PASA por ahí (no de una al arrancar).
                if (trasera != null) StartCoroutine(RichardThroughTrasera(richard, trasera));
                for (int i = richardToFreezerWaypoints.Length - 1; i >= 0; i--)
                    yield return WalkTo(richard, richardToFreezerWaypoints[i], speed: 1.6f, stopDist: 0.35f);
                if (behindCounterPos != Vector3.zero) Teleport(richard, behindCounterPos);
                FaceTowards(richard, counterPos);
            }

            yield return Say("Richard: Listo, muchachos. Pasen por caja.", 2.4f);

            // el jugador (o el perro) va al MOSTRADOR a pagar: E cerca del mostrador.
            _hint = "Andá al mostrador a pagar (E)";
            bool paid = false;
            while (!paid)
            {
                float best = NearestPartyDist(counterPos);
                var kb = Keyboard.current;
                if (best <= payRange && kb != null && kb.eKey.wasPressedThisFrame && !SettingsMenu.IsOpen) paid = true;
                yield return null;
            }
            _hint = "";
            yield return Say("Vos: Tomá, quedate con el vuelto.", 1.8f);

            // GRITO del baño: la chica (el viejo verde le quiso tocar el culo).
            PlayScreamAt(chicaBathroomInside);
            yield return Say("Chica: ¡AAAAAH!", 1.2f);
            yield return Say("¡Un grito viene del baño!", 1.8f);

            // apenas escuchan el grito, corren SOLO los dos amigos (MaleCasual desde la tienda,
            // MaleGreenJkt desde el auto). Richard NO va. Cada uno por SU ruta (no atraviesa cosas).
            if (op != null)
            {
                if (casualToBathWaypoints != null && casualToBathWaypoints.Length > 0)
                    StartCoroutine(RunNpcPath(op.friendMaleCasual, casualToBathWaypoints));
                else RunNpc(op.friendMaleCasual);

                if (greenJktToBathWaypoints != null && greenJktToBathWaypoints.Length > 0)
                    StartCoroutine(RunNpcPath(op.friendMaleGreenJkt, greenJktToBathWaypoints));
                else RunNpc(op.friendMaleGreenJkt);
            }

            // el VIEJO VERDE se ubica adentro del baño (owner) y espera atrás de la chica. Le
            // DESACTIVO el FriendWander para que NO se mueva solo (owner: "no para todos lados").
            var viejoGo = GameObject.Find("CreepyOldMan");
            if (viejoGo != null)
            {
                var fw = viejoGo.GetComponent<FriendWander>();
                if (fw != null) fw.enabled = false;
                Teleport(viejoGo.transform, viejoStartPos);
                FaceTowards(viejoGo.transform, chicaBathroomInside);
            }

            // la chica espera detrás de la puerta. Cuando los jugadores PASAN el trigger, sale del
            // baño; 1 s después sale el VIEJO VERDE y hace su recorrido.
            _hint = "¡Corré al baño!";
            yield return new WaitUntil(() => NearestPartyDist(chicaTriggerPos) <= chicaTriggerRange);
            _hint = "";

            if (op != null && op.friendFemaleSec != null)
                StartCoroutine(ChicaExitBathroom(op.friendFemaleSec));

            yield return new WaitForSeconds(1f);   // el viejo sale UN SEGUNDO después
            if (viejoGo != null)
                yield return ViejoExitAndConfront(viejoGo.transform);
        }

        // El viejo verde sale del baño (la puerta se abre/cierra), se para y CHIFLA con un piropo;
        // la chica lo putea; MaleCasual se le pone enfrente y lo echa; el viejo se va al auto rojo.
        IEnumerator ViejoExitAndConfront(Transform viejo)
        {
            // owner: la puerta del baño se abre cuando el viejo LLEGA (no de una: la corrutina de
            // la chica la cerraba antes). Abre al acercarse a la puerta y cierra al pasar.
            var bath = FindEntranceDoor();
            if (bath != null) StartCoroutine(OpenWhenNear(viejo, bath, chicaDoorway, 2.2f));
            if (viejoWaypoints != null)
                foreach (var wp in viejoWaypoints)
                    yield return WalkTo(viejo, wp, speed: 1.4f, stopDist: 0.3f);
            yield return WalkTo(viejo, viejoStandPos, speed: 1.4f, stopDist: 0.25f); // se para acá
            if (bath != null) StartCoroutine(CloseWhenClear(viejo, chicaDoorway, bath, 2f));

            // chifla + piropo (mirando a la chica)
            var chica = op != null ? op.friendFemaleSec : null;
            if (chica != null) FaceTowards(viejo, chica.position);
            PlayWhistle(viejo.position);
            yield return FocusSay("Viejo verde: *fiu-fiu*... Qué buena que estás, pendeja.", 2.8f, viejo.position);

            // la chica lo mira y le contesta (cámara a la chica)
            if (chica != null) FaceTowards(chica, viejo.position);
            yield return FocusSay("Chica: Viejo verde desubicado.", 2.2f, chica != null ? chica.position : viejo.position);

            // MaleCasual se le pone ENFRENTE y lo echa
            var casual = op != null ? op.friendMaleCasual : null;
            if (casual != null)
            {
                yield return WalkTo(casual, casualConfrontPos, speed: 2.4f, stopDist: 0.25f);
                FaceTowards(casual, viejo.position);
                FaceTowards(viejo, casual.position);
            }
            yield return FocusSay("Amigo: Tomatela, viejo.", 2f, casual != null ? casual.position : viejo.position);

            // el viejo YA se va -> el baño queda LIBRE para los jugadores (owner).
            if (_bathDoor != null) _bathDoor.locked = false;

            // el viejo se va hasta el AUTO ROJO y, al llegar a la puerta, DESAPARECE (se subió y se fue).
            if (viejoToCarWaypoints != null)
                foreach (var wp in viejoToCarWaypoints)
                    yield return WalkTo(viejo, wp, speed: 1.5f, stopDist: 0.4f);
            if (viejo != null) Destroy(viejo.gameObject);
        }

        // chiflido del viejo (placeholder: si hay Resources/whistle lo usa, si no queda mudo).
        void PlayWhistle(Vector3 at)
        {
            var clip = Resources.Load<AudioClip>("whistle");
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, 1f);
        }

        // ── ETAPA 7: "vámonos" -> los amigos caminan al auto y se suben ATRÁS; el jugador maneja.
        IEnumerator Stage7_Board()
        {
            yield return Say("Amigo: Bueno, vámonos a la puta de acá.", 2.4f);

            var car = Object.FindFirstObjectByType<CarController>();
            if (car == null) { Debug.LogWarning("[YPF] Etapa 7: no encontré el auto"); yield break; }

            Transform casual = op != null ? op.friendMaleCasual : null;
            Transform green  = op != null ? op.friendMaleGreenJkt : null;
            Transform chica  = op != null ? op.friendFemaleSec : null;

            // cada amigo camina el CAMINO COMÚN y después SU cola hasta la puerta (en paralelo),
            // y al llegar se sienta en su asiento.
            // MaleCasual + el negro van por la puerta IZQUIERDA; la chica por la DERECHA.
            StartCoroutine(WalkThenSeat(casual, Concat(friendsCommonPath, leftDoorTail),  car, op != null ? op.rearLeftLocal  : Vector3.zero));
            StartCoroutine(WalkThenSeat(green,  Concat(friendsCommonPath, leftDoorTail),  car, op != null ? op.rearMidLocal   : Vector3.zero));
            StartCoroutine(WalkThenSeat(chica,  Concat(friendsCommonPath, rightDoorTail), car, op != null ? op.rearRightLocal : Vector3.zero));

            // esperar a que los 3 estén sentados (o timeout de seguridad)
            float t = 0f;
            while (t < 14f && !(IsSeated(casual) && IsSeated(green) && IsSeated(chica)))
            { t += Time.deltaTime; yield return null; }

            yield return Say("Amigo: Dale, arrancá. Al campamento.", 2.4f);
            _hint = "Subite al auto (E) y manejá hasta el campamento";
        }

        // sienta un NPC en el auto (lo parentea + pose sentada + le apaga el FriendWander).
        void SeatInCar(Transform friend, CarController car, Vector3 localPos)
        {
            if (friend == null || car == null) return;
            friend.SetParent(car.transform, false);
            friend.localRotation = Quaternion.identity;
            friend.localPosition = localPos;
            var fw = friend.GetComponent<FriendWander>(); if (fw != null) fw.enabled = false;
            var anim = friend.GetComponent<HumanWalkAnim>(); if (anim != null) anim.seated = true;
        }

        // camina 'npc' por 'path' y al terminar lo sienta en el auto (asiento localPos).
        IEnumerator WalkThenSeat(Transform npc, Vector3[] path, CarController car, Vector3 localPos)
        {
            if (npc == null) yield break;
            if (path != null)
                foreach (var wp in path)
                    yield return WalkTo(npc, wp, speed: 3.2f, stopDist: 0.4f);
            SeatInCar(npc, car, localPos);
        }

        // true si el NPC ya está sentado (HumanWalkAnim.seated) o no existe.
        static bool IsSeated(Transform npc)
        {
            if (npc == null) return true;
            var anim = npc.GetComponent<HumanWalkAnim>();
            return anim != null && anim.seated;
        }

        // concatena dos arrays de puntos (camino común + cola de cada amigo).
        static Vector3[] Concat(Vector3[] a, Vector3[] b)
        {
            a = a ?? new Vector3[0]; b = b ?? new Vector3[0];
            var r = new Vector3[a.Length + b.Length];
            a.CopyTo(r, 0); b.CopyTo(r, a.Length);
            return r;
        }

        void RunNpc(Transform t) { if (t != null) StartCoroutine(RunNpcTo(t, bathroomGatherPos)); }

        // corre un NPC hasta 'dest' (WalkTo más rápido).
        IEnumerator RunNpcTo(Transform npc, Vector3 dest)
        {
            yield return WalkTo(npc, dest, speed: 3.4f, stopDist: 0.5f);
        }

        // corre un NPC por una RUTA de waypoints (para no atravesar paredes).
        IEnumerator RunNpcPath(Transform npc, Vector3[] path)
        {
            if (npc == null || path == null) yield break;
            foreach (var wp in path)
                yield return WalkTo(npc, wp, speed: 3.4f, stopDist: 0.5f);
        }

        // la chica sale del baño (abre la puerta) y corre hasta juntarse con el grupo.
        IEnumerator ChicaExitBathroom(Transform chica)
        {
            var bath = FindEntranceDoor();       // puerta de entrada del baño
            if (bath != null) bath.SetOpen(true);
            if (chicaExitWaypoints != null)
                foreach (var wp in chicaExitWaypoints)
                    yield return WalkTo(chica, wp, speed: 3.2f, stopDist: 0.4f);
            if (bath != null) StartCoroutine(CloseWhenClear(chica, chicaDoorway, bath, 2f));
        }

        // distancia horizontal MÍNIMA de la party (cámara activa / persona / perro) a 'p'.
        float NearestPartyDist(Vector3 p)
        {
            Transform c  = ActiveCam();
            Transform pl = (op != null && op.player != null) ? op.player.transform : null;
            Transform dg = (op != null && op.dog != null)    ? op.dog.transform    : null;
            float best = float.MaxValue;
            if (c  != null) best = Mathf.Min(best, Vector3.Distance(Flat(c.position),  Flat(p)));
            if (pl != null) best = Mathf.Min(best, Vector3.Distance(Flat(pl.position), Flat(p)));
            if (dg != null) best = Mathf.Min(best, Vector3.Distance(Flat(dg.position), Flat(p)));
            return best;
        }

        // grito del baño (placeholder gasp.wav; podés poner un scream.wav en Resources).
        void PlayScreamAt(Vector3 at)
        {
            var clip = Resources.Load<AudioClip>("scream") ?? Resources.Load<AudioClip>("gasp");
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, 0.45f);   // owner: más bajo
        }

        // orienta 't' horizontalmente hacia 'target'.
        static void FaceTowards(Transform t, Vector3 target)
        {
            Vector3 look = target - t.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(look.normalized);
        }

        // altura del piso bajo 'p' (raycast hacia abajo). Fallback aproximado si no pega.
        float GroundYAt(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, out var hit, 15f))
                return hit.point.y;
            return p.y - 1.3f;
        }

        // rata: modelo PS1 real (Resources/Rat/RatModel). La envuelvo en un ROOT a nivel de piso
        // (el correteo mueve el root y le fija la Y al piso), con el modelo adentro auto-escalado a
        // ~0.18 m y apoyado (su base en el origen del root). Si falta el asset, cae a un cilindro.
        const float RatSizeMeters = 0.65f;   // owner: mucho más grandes
        GameObject CreateRat(Vector3 at)
        {
            var root = new GameObject("Rata");
            root.transform.position = at;

            var pf = Resources.Load<GameObject>("Rat/RatModel");
            if (pf != null)
            {
                var model = Object.Instantiate(pf);
                model.name = "RatModel";
                foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                model.transform.SetParent(root.transform, false);

                var rends = model.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (longest > 1e-4f) model.transform.localScale *= (RatSizeMeters / longest);

                    // recentrar en X/Z sobre el root y apoyar la base en el piso (root.y)
                    b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    model.transform.position += new Vector3(root.transform.position.x - b.center.x,
                                                            root.transform.position.y - b.min.y,
                                                            root.transform.position.z - b.center.z);
                }
                AddRatOutline(model);
                return root;
            }

            // fallback: cilindro (como antes) dentro del root
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var col = cyl.GetComponent<Collider>(); if (col != null) Destroy(col);
            cyl.transform.SetParent(root.transform, false);
            cyl.transform.localScale = new Vector3(0.12f, 0.09f, 0.22f);
            var r = cyl.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.18f, 0.15f, 0.13f);
            return root;
        }

        // contorno amarillo llamativo (casco invertido): shader dedicado Custom/RatOutline (expande
        // por normales + Cull Front) -> solo se ve el borde, no tapa la rata.
        void AddRatOutline(GameObject model)
        {
            Shader sh = Shader.Find("Custom/RatOutline");
            if (sh == null) return;   // sin el shader, no dibujo contorno (mejor eso que taparla)
            var mat = new Material(sh);
            mat.SetColor("_Color", new Color(1f, 0.85f, 0.15f));   // amarillo
            mat.SetFloat("_Width", 0.03f);
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var o = new GameObject("Outline");
                o.transform.SetParent(mf.transform, false);   // escala 1: la expansión la hace el shader
                o.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                o.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        // punto random dentro del área del parking (para el correteo de las ratas), pegado al piso.
        Vector3 RandomRatPoint()
        {
            Vector3 p = new Vector3(
                ratAreaCenter.x + Random.Range(-ratAreaHalfSize.x, ratAreaHalfSize.x),
                0f,
                ratAreaCenter.z + Random.Range(-ratAreaHalfSize.z, ratAreaHalfSize.z));
            p.y = GroundYAt(p);
            return p;
        }

        // le pone una "gorra" (cilindro de color) arriba de la cabeza para el gag del otro playero.
        void AddCap(Transform richard)
        {
            var b = RendererBounds(richard.gameObject);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "GorraDisfraz";
            var col = cap.GetComponent<Collider>(); if (col != null) Destroy(col);
            cap.transform.SetParent(richard, true);   // sigue a Richard
            cap.transform.position = new Vector3(b.center.x, b.max.y + 0.03f, b.center.z);
            cap.transform.rotation = Quaternion.identity;
            // escala en MUNDO ~ gorra; compenso la escala del padre para que no se deforme
            Vector3 ls = richard.lossyScale;
            cap.transform.localScale = new Vector3(0.16f / Mathf.Max(1e-4f, ls.x),
                                                   0.05f / Mathf.Max(1e-4f, ls.y),
                                                   0.16f / Mathf.Max(1e-4f, ls.z));
            var r = cap.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.15f, 0.35f, 0.75f); // gorra azul (distinta)
        }

        // AABB de mundo de todos los renderers de 'go'.
        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // objeto coleccionable: cubito con brillo para que se vea en la estantería (sin collider
        // que trabe; se agarra por cercanía en ShoppingLoop).
        GameObject CreateStoreItem(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "StoreItem";
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * 0.18f;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(1f, 0.85f, 0.2f);
                if (r.material.HasProperty("_EmissionColor"))
                {
                    r.material.EnableKeyword("_EMISSION");
                    r.material.SetColor("_EmissionColor", new Color(0.9f, 0.7f, 0.1f));
                }
            }
            return go;
        }

        // Traba/destraba SOLO la E de la puerta de la TIENDA (owner: la OFICINA no se toca, y la
        // TRASERA queda SIEMPRE trabada -> solo la abre Richard por código, nunca el jugador). NO
        // cierra las puertas (SetOpen) para no pelear con la apertura de Richard -> el bloqueo
        // físico lo hace la barrera invisible.
        void LockPlayerIn(bool trap)
        {
            var store = FindStoreDoor();
            if (store != null) { store.locked = trap; if (trap) store.knockToUnlock = false; }
            // la trasera se mantiene trabada siempre (no la tocamos acá)
            if (_traseraDoor != null) { _traseraDoor.locked = true; _traseraDoor.knockToUnlock = false; }
            Debug.Log($"<color=yellow>[YPF] LockPlayerIn({trap}) -> E de tienda {(trap ? "trabada" : "libre")}; trasera SIEMPRE trabada</color>");
        }

        // Ruta de Richard del rociador a ATRÁS DEL AUTO (owner). El último = atrás del auto (tanque).
        Vector3[] richardBackWaypoints = new Vector3[]
        {
            new Vector3(464.2089f, 17.46277f, -33.06145f),
            new Vector3(463.3911f, 17.46277f, -34.64084f),
            new Vector3(465.788f,  17.07963f, -37.44016f), // atrás del auto
        };

        // Ruta de RETORNO: del auto de vuelta a la tienda y DETRÁS DEL MOSTRADOR (Etapa 5). Reusa
        // los puntos de la ruta de nafta al revés + entra por la puerta de la tienda + mostrador.
        Vector3[] richardReturnWaypoints = new Vector3[]
        {
            new Vector3(463.3911f, 17.46277f, -34.64084f),
            new Vector3(464.2089f, 17.46277f, -33.06145f),
            new Vector3(463.1381f, 17.46277f, -32.08751f),
            new Vector3(463.0858f, 17.07963f, -31.51123f),
            new Vector3(462.9436f, 17.25815f, -19.30591f), // puerta de la tienda
            // owner: rodea el mostrador (NO lo cruza) para entrar por atrás de la mesada:
            new Vector3(462.8418f, 17.2834f,  -18.75983f),
            new Vector3(462.9486f, 17.25815f, -11.0586f),
            new Vector3(467.8743f, 17.25814f, -10.46315f), // rodea sin cruzar la pared (owner)
            new Vector3(466.6333f, 17.25815f, -15.84829f), // detrás del mostrador
        };

        // ── ETAPA 3: screamer de Richard (estilo Fears to Fathom) ────────────
        // Al acercarte a Richard (sentado en la oscuridad), salta el screamer: arranca NEGRO
        // (silueta) y se va aclarando la textura, con un sting + el jadeo del susto del
        // personaje, mientras la cámara zoomea (FOV) hacia él.
        // owner: el screamer se DISPARA cuando el jugador (o el perro) cruza una LÍNEA a esta
        // profundidad -- NO un punto: pases por el medio o por un costado, salta igual. La
        // línea es el plano en screamTriggerPos perpendicular a screamApproachDir (la dirección
        // en la que avanzás hacia el playero; por defecto -X, ajustá si entrás por otro eje).
        public Vector3 screamTriggerPos = new Vector3(459.3862f, 17.25814f, -2.415934f);
        public Vector3 screamApproachDir = new Vector3(-1f, 0f, 0f);
        // owner: el ZOOM a Richard sentado va cuando ya PASASTE la puerta (adentro), no al
        // golpear. Línea (mismo criterio) apenas cruzás la puerta de la oficina. (0,0,0) = sin
        // setear -> se saltea el zoom (paramelo con el TEST_PLAYER apenas adentro y pasámelo).
        public Vector3 peekTriggerPos = new Vector3(466.1634f, 17.25814f, -3.836614f);
        public float peekRadius = 0.8f;   // radio chico: solo salta cuando ya estás JUSTO en el punto (adentro)
        // owner: "que aparezca el playero screamer pero PARADO en esa posición" -- se levanta
        // de la silla a este punto/yaw al saltar el screamer. (Un poco más atrás que su punto
        // original, owner: "ponelo un poquito más atrás".)
        public Vector3 richardStandPos = new Vector3(457.9297f, 17.25814f, -2.36265f);
        public float richardStandYaw = 89.992f;
        IEnumerator Stage3_RichardScreamer()
        {
            var richardGo = GameObject.Find("Playero_Richard");
            if (richardGo == null) { Debug.LogWarning("[YpfStory] Etapa 3: no encontré a Richard (Playero_Richard)."); yield break; }
            Transform richard = richardGo.transform;
            var player = (op != null && op.player != null) ? op.player.transform : null;
            var dog = (op != null && op.dog != null) ? op.dog.transform : null;

            // 1) apenas ENTRÁS a la oficina -> zoom/asomada a Richard sentado. Trigger LOCALIZADO
            // (radio alrededor del punto de la oficina), NO un plano: si fuera plano, moviéndote
            // en la tienda (misma X, otra Z) igual se disparaba (owner). Y con la puerta ABIERTA.
            if (peekTriggerPos != Vector3.zero && _officeDoor != null)
            {
                while (!(_officeDoor.IsOpen && Vector3.Distance(Flat(player.position), Flat(peekTriggerPos)) <= peekRadius))
                    yield return null;
                yield return PeekAtSeatedRichard();
            }

            // 2) al cruzar la línea del screamer (toda la línea, por cualquier lado) -> salta.
            while (!CrossedLine(screamTriggerPos, screamApproachDir, player, dog))
                yield return null;

            // owner: "que se frene el jugador un segundo" cuando salta el screamer -- LockLook
            // bloquea el caminar (y fija la mirada) ~1s: queda paralizado del susto.
            var freezeExplorer = (op != null && op.player != null) ? op.player.GetComponent<MapExplorer>() : null;
            if (freezeExplorer != null) freezeExplorer.LockLook(1f, 4f);

            var cam = ActiveCamera();
            var camT = cam != null ? cam.transform : null;

            // SE LEVANTA: lo desparento de la silla, lo paro en la posición/yaw del owner, escala
            // de parado, y le enderezo piernas + cuelgo brazos (pose de pie).
            richard.SetParent(null, true);
            richard.position = richardStandPos;
            richard.rotation = Quaternion.Euler(0f, richardStandYaw, 0f);
            richard.localScale = Vector3.one * 1.161092f;
            FolkloreArchives.HumanWalkAnim.PoseStandingStatic(richard,
                new[] { "shoulder.L", "shoulder.R" }, new[] { "foearm.L", "foearm.R" },
                new[] { "thigh.L", "thigh.R" }, new[] { "calf.L", "calf.R" });

            // material de Richard -> instancia y arranca NEGRO (silueta)
            var smr = richardGo.GetComponentInChildren<SkinnedMeshRenderer>();
            Material mat = smr != null ? smr.material : null;
            Color baseCol = (mat != null && mat.HasProperty("_BaseColor")) ? mat.GetColor("_BaseColor") : Color.white;
            SetMatColor(mat, Color.black);

            // SONIDO: sting del screamer + jadeo/aliento del susto (Resources: "jumpscare", "gasp")
            PlayResource("jumpscare", richard.position, 1f);
            PlayResource("gasp", camT != null ? camT.position : richard.position, 1f);

            // CÁMARA: punch de zoom (baja el FOV rápido hacia el screamer)
            float fov0 = cam != null ? cam.fieldOfView : 60f;
            float t = 0f;
            while (t < 1f && cam != null) { t += Time.deltaTime / 0.12f; cam.fieldOfView = Mathf.Lerp(fov0, fov0 - 18f, Mathf.Clamp01(t)); yield return null; }

            // FADE: negro -> textura (la silueta se aclara)
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 1.3f;
                SetMatColor(mat, Color.Lerp(Color.black, baseCol, Mathf.Clamp01(t)));
                yield return null;
            }
            SetMatColor(mat, baseCol);

            yield return new WaitForSeconds(0.6f);

            // resolver: devolver el FOV a lo normal
            t = 0f;
            while (t < 1f && cam != null) { t += Time.deltaTime / 0.45f; cam.fieldOfView = Mathf.Lerp(fov0 - 18f, fov0, Mathf.Clamp01(t)); yield return null; }
            if (cam != null) cam.fieldOfView = fov0;
        }

        // owner: punto donde el jugador se para a GOLPEAR la puerta de la oficina (lo da parando
        // el TEST_PLAYER ahí). La puerta a abrir = la SwingDoor más cercana a este punto.
        // (0,0,0) = sin setear -> usa la puerta más cercana a Richard (aproximado).
        public Vector3 officeKnockPos = new Vector3(0f, 0f, 0f);
        public float knockRange = 2.0f;

        // ── ETAPA 1c: el otro amigo (MaleGreenJkt) se queda al lado del auto ──
        // owner: spawnea al bajar, camina a un punto y queda mirando el auto (esperando).
        Vector3 greenJktSpawn = new Vector3(464.2825f, 17.07963f, -37.4811f);
        // Y ajustada a mano: la losa visual está ~17.22 (el collider da 17.38 = flotaba, el
        // terreno 17.07 = hundido). Afiná esta Y si queda flotando/hundido un toque.
        Vector3 greenJktStand = new Vector3(465.6328f, 17.22f, -34.2519f);
        IEnumerator Stage1c_GreenJktStaysByCar()
        {
            var friend = op != null ? op.friendMaleGreenJkt : null;
            if (friend == null) yield break;
            // ya lo teletransportamos al spawn en Begin; camina a su punto. NO forzamos la Y del
            // punto (17.46) porque queda por encima del piso real ahí (flotaba) -- el WalkTo ya
            // lo deja apoyado en el piso.
            yield return WalkTo(friend, greenJktStand, speed: 1.6f, stopDist: 0.3f);
            Teleport(friend, greenJktStand); // asentar EXACTO en la Y ajustada a mano (ni collider ni terreno dan justo)
            // queda mirando al auto.
            var car = Object.FindFirstObjectByType<CarController>();
            if (car != null)
            {
                Vector3 look = car.transform.position - friend.position; look.y = 0f;
                if (look.sqrMagnitude > 1e-4f) friend.rotation = Quaternion.LookRotation(look.normalized);
            }
        }

        // ── ETAPA 1b: un amigo abre la puerta de la TIENDA y entra (antes del golpeo) ──
        // owner: la tienda tiene su PROPIA puerta, ANTES de la oficina de Richard. El amigo
        // (MaleGreenJkt) camina del auto a esa puerta, la abre y entra; el jugador NO puede
        // pasar la puerta de la tienda hasta que el amigo la abre.
        public Vector3 storeDoorPos = new Vector3(462.687f, 17.25814f, -19.75606f); // wp4 = la puerta
        // Ruta del amigo (MaleCasual): del volante -> rodea el auto -> frente tienda -> puerta ->
        // adentro. Se re-mapea de cero (owner). Pasar los puntos EN ORDEN con el TEST_PLAYER.
        Vector3[] friendStoreWaypoints = new Vector3[]
        {
            new Vector3(462.9489f, 17.07963f, -41.42895f), // spawn (puerta del volante)
            new Vector3(466.2572f, 17.07963f, -42.05516f), // rodea el auto
            new Vector3(469.2768f, 17.07963f, -39.24262f), //   "
            new Vector3(468.7429f, 17.07963f, -31.77548f), // hacia la tienda
            new Vector3(465.1657f, 17.07963f, -25.3138f),  // frente a la puerta
            new Vector3(463.1372f, 17.25814f, -20.0286f),  // puerta / apenas adentro
            new Vector3(462.9861f, 17.25841f, -17.75892f), // derecho pasando la puerta
            new Vector3(457.6115f, 17.25815f, -18.5543f),  // ya adentro
            new Vector3(454.001f,  17.25815f, -18.17559f), // esquiva la estantería
            new Vector3(453.8044f, 17.25815f, -16.07737f), // adentro (donde queda el amigo)
        };
        Vector3 friendStoreInside = Vector3.zero;        // (wp4 ya es la entrada; sin punto extra por ahora)

        IEnumerator Stage1b_FriendEntersStore()
        {
            var friend = op != null ? op.friendMaleCasual : null;
            var store = FindStoreDoor();
            if (friend == null || store == null || friendStoreWaypoints == null || friendStoreWaypoints.Length == 0)
            {
                Debug.LogWarning($"[YpfStory] Etapa 1b: falta amigo={(friend != null)}, puertaTienda={(store != null)} o waypoints -- (paso salteado).");
                if (store != null) { store.locked = false; store.SetOpen(true); } // no trabar el juego
                _friendPassedStoreDoor = true; // no colgar la espera del golpeo
                yield break;
            }

            // trabar la puerta de la tienda: el jugador no puede abrirla hasta que el amigo la abra.
            store.locked = true;
            // se abre cuando el AMIGO llega
            StartCoroutine(OpenStoreWhenFriendArrives(friend, store));

            // ya lo teletransportamos al wp[0] (spawn) en Begin; camina del [1] en adelante.
            for (int i = 1; i < friendStoreWaypoints.Length; i++)
                yield return WalkTo(friend, friendStoreWaypoints[i], speed: 1.6f, stopDist: 0.35f);

            store.locked = false; store.SetOpen(true); // asegurar abierta y destrabada (ya entró)
            if (friendStoreInside != Vector3.zero)
                yield return WalkTo(friend, friendStoreInside, speed: 1.5f, stopDist: 0.2f);
        }

        // la puerta de la TIENDA = la SwingDoor más cercana al punto que dio el owner (o, si no
        // lo dio, no hace nada -> paso salteado).
        SwingDoor FindStoreDoor()
        {
            if (storeDoorPos == Vector3.zero) return null;
            SwingDoor best = null; float bd = float.MaxValue;
            foreach (var d in Object.FindObjectsByType<SwingDoor>(FindObjectsSortMode.None))
            {
                float dist = Vector3.Distance(DoorCenter(d), storeDoorPos);
                if (dist < bd) { bd = dist; best = d; }
            }
            return best;
        }

        // abre la puerta de la tienda (destrabándola) cuando el amigo se acerca al hueco.
        IEnumerator OpenStoreWhenFriendArrives(Transform friend, SwingDoor store)
        {
            int guard = 0;
            Vector3 c = DoorCenter(store);
            while (friend != null && store != null && guard++ < 6000)
            {
                if (Vector3.Distance(Flat(friend.position), Flat(c)) <= 3.0f)
                { store.locked = false; store.SetOpen(true); _friendPassedStoreDoor = true; yield break; }
                yield return null;
            }
        }

        // ── ETAPA 2: golpear la puerta de la oficina y entrar ────────────────
        // Vos + Rufus van a la oficina (donde está Richard). Al acercarte a la puerta te deja
        // GOLPEAR (E); como nadie abre, se destraba para que entres normal (E) -> adentro está
        // Richard en la oscuridad (Etapa 3: screamer).
        IEnumerator Stage2_KnockOffice()
        {
            var player = (op != null && op.player != null) ? op.player.transform : null;
            // la puerta ya la trabamos en LockOfficeDoorsUpfront (desde que bajan). Reusamos esa.
            var office = _officeDoor != null ? _officeDoor : FindOfficeDoor(
                officeKnockPos != Vector3.zero ? officeKnockPos
                : (GameObject.Find("Playero_Richard") is GameObject r ? r.transform.position : chicaBathroomInside));
            if (player == null || office == null)
            {
                Debug.LogWarning($"[YpfStory] Etapa 2: falta player={(player != null)} u oficina={(office != null)}.");
                yield break;
            }

            // AHORA (el amigo ya pasó la tienda) pasa a modo GOLPEAR: SwingDoor muestra "[E]
            // Golpear" SOLO apuntándola, y avisa por onKnock al apretar E. Estaba trabada
            // (ni abrir) desde que bajaron.
            _officeDoor = office;
            office.locked = true;
            office.knockToUnlock = true;
            bool knocked = false;
            office.onKnock = () => knocked = true;
            Debug.Log($"<color=yellow>[YpfStory] Etapa 2 activa: puerta oficina='{office.name}' -- apuntala y golpeá (E).</color>");

            while (!knocked) yield return null;
            office.knockToUnlock = false;
            office.onKnock = null;

            // golpea 3 veces
            yield return Knock(DoorCenter(office));

            // ...y nadie responde (el zoom a Richard NO va acá: se hace recién al PASAR la
            // puerta, en la Etapa 3 -- si no, zoomea a la compu a través de la pared).
            _hint = "Nadie responde...";
            yield return new WaitForSeconds(1.4f);
            _hint = "";

            // se destraba: ahora entrás a la oficina con E (adentro Richard en la oscuridad).
            office.locked = false;
            Debug.Log("<color=yellow>[YpfStory] Etapa 2 lista (golpeó, puerta destrabada) -> arranca Etapa 3.</color>");
        }

        // ── ETAPA 1: dispersión ──────────────────────────────────────────────
        // La chica camina al baño; los 2 amigos ya quedaron parados al lado del auto
        // (OpeningDriveSequence.StandFriend). Vos + Rufus ya tienen el control (ExitRoutine).
        IEnumerator Stage1_Disperse()
        {
            var chica = op != null ? op.friendFemaleSec : null;
            if (chica == null) { Debug.LogWarning("[YpfStory] Etapa 1: no encontré a la chica (Friend_FemaleSec)."); yield break; }

            // (ya la teletransportamos al punto [0] en Begin, en el frame de la bajada.)

            // Puerta de ENTRADA del baño = la SwingDoor NO-cubículo más cercana al punto de
            // adentro. Del diagnóstico: es "Door_01" en ~(433.19, -2.98). El HUECO cae en su
            // transform (x≈433), NO en la recta punto4->adentro (que cruza la pared en x≈442),
            // por eso hay que rutear a la chica PASANDO por el hueco de la puerta.
            var door = FindEntranceDoor();
            if (door != null)
            {
                _openedDoor = door;
                // el punto por el que ROUTEA (el hueco): si el owner dio el umbral, ese; si no,
                // el transform de la puerta (aproximado, cae a un costado).
                _doorwayPoint = chicaDoorway != Vector3.zero ? chicaDoorway : door.transform.position;
                _doorwayPoint.y = chica.position.y;
                _haveDoorway = true;
                // abre según la distancia al HUECO (por donde pasa), no al transform de la
                // puerta (que cae a un costado y la chica nunca se le acerca).
                StartCoroutine(OpenWhenNear(chica, door, _doorwayPoint, 3.2f));
            }

            // 1) recorrer la ruta despejada. Arranca en el punto [1] (ya está en [0]).
            if (chicaWaypoints != null)
                for (int i = 1; i < chicaWaypoints.Length; i++)
                    yield return WalkTo(chica, chicaWaypoints[i], speed: 1.6f, stopDist: 0.35f);

            // 2) entrar POR EL HUECO de la puerta y recién ahí ir al punto de adentro -- así no
            // dobla de golpe atravesando la pared al costado del marco.
            if (_haveDoorway)
                yield return WalkTo(chica, _doorwayPoint, speed: 1.3f, stopDist: 0.2f);

            // cerrar la puerta recién cuando la chica ya se DESPEGÓ del hueco (~1.8 m), para no
            // cerrarle encima (owner: "se cierra con la chica encima y queda atravesando"), pero
            // sin esperar a que llegue al fondo (no se siente lenta).
            if (_openedDoor != null)
                StartCoroutine(CloseWhenClear(chica, _doorwayPoint, _openedDoor, 1.8f));

            // 3) seguir hasta el punto EXACTO de adentro (la puerta se cierra sola detrás).
            yield return WalkTo(chica, chicaBathroomInside, speed: 1.3f, stopDist: 0.15f);
            Teleport(chica, chicaBathroomInside);      // asentar exacto
        }

        // cierra la puerta cuando 'who' ya se alejó 'clearDist' del hueco (no le cierra encima).
        IEnumerator CloseWhenClear(Transform who, Vector3 doorway, SwingDoor door, float clearDist)
        {
            int guard = 0;
            while (who != null && door != null && guard++ < 3000)
            {
                if (Vector3.Distance(Flat(who.position), Flat(doorway)) >= clearDist) { door.SetOpen(false); yield break; }
                yield return null;
            }
            if (door != null) door.SetOpen(false);
        }

        // Richard abre la puerta de la tienda al LLEGAR y la cierra al PASAR (owner: no la
        // atraviesa). SetOpen es por código -> anda aunque siga trabada para el jugador.
        IEnumerator RichardThroughStoreDoor(Transform richard, SwingDoor store)
        {
            yield return OpenWhenNear(richard, store, storeDoorPos, 2.2f);
            yield return CloseWhenClear(richard, storeDoorPos, store, 2.2f);
            _richardPassedStore = true;   // owner: recién ahora aparecen los objetos de estantería
        }

        // Richard abre la TRASERA cuando llega y la cierra al pasar (owner: no de una al arrancar).
        IEnumerator RichardThroughTrasera(Transform richard, SwingDoor trasera)
        {
            yield return OpenWhenNear(richard, trasera, trasereDoorPos, 2.2f);
            yield return CloseWhenClear(richard, trasereDoorPos, trasera, 2.2f);
        }

        // puerta de ENTRADA del baño = la SwingDoor NO-cubículo (ni "puerta" de la casa lejana)
        // más cercana al punto de adentro.
        SwingDoor FindEntranceDoor()
        {
            SwingDoor best = null; float bd = float.MaxValue;
            foreach (var d in Object.FindObjectsByType<SwingDoor>(FindObjectsSortMode.None))
            {
                string n = d.name.ToLowerInvariant();
                if (n.Contains("cubicle") || n.Contains("toilet") || n.Contains("puerta")) continue;
                float dist = Vector3.Distance(d.transform.position, chicaBathroomInside);
                if (dist < bd) { bd = dist; best = d; }
            }
            return best;
        }

        // abre la puerta cuando 'who' se acerca a menos de 'range' (horizontal) del punto
        // 'point' (el hueco), una sola vez.
        IEnumerator OpenWhenNear(Transform who, SwingDoor door, Vector3 point, float range)
        {
            int guard = 0;
            point.y = 0f;
            while (who != null && door != null && guard++ < 6000)
            {
                Vector3 a = who.position; a.y = 0f;
                if (Vector3.Distance(a, point) <= range) { door.SetOpen(true); yield break; }
                yield return null;
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // puerta de la OFICINA = la SwingDoor NO-cubículo más cercana a 'refPos'.
        SwingDoor FindOfficeDoor(Vector3 refPos)
        {
            SwingDoor best = null; float bd = float.MaxValue;
            foreach (var d in Object.FindObjectsByType<SwingDoor>(FindObjectsSortMode.None))
            {
                string n = d.name.ToLowerInvariant();
                if (n.Contains("cubicle") || n.Contains("toilet") || n.Contains("puerta")) continue;
                float dist = Vector3.Distance(DoorCenter(d), refPos);
                if (dist < bd) { bd = dist; best = d; }
            }
            return best;
        }

        // centro real de la puerta (bounds del renderer -- el transform cae a un costado).
        static Vector3 DoorCenter(SwingDoor d)
        {
            var r = d.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : d.transform.position;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // Asomarse + zoom hacia el playero SENTADO (al golpear). Apunta el cuerpo (yaw) y la
        // cámara (pitch) hacia Richard usando el sistema de mirada del MapExplorer (LockLook,
        // así no lo pisa el mouse), y hace un zoom de FOV. Solo si controlás a la persona.
        IEnumerator PeekAtSeatedRichard()
        {
            var richardGo = GameObject.Find("Playero_Richard");
            var cam = ActiveCamera();
            if (richardGo == null || cam == null) yield break;
            Vector3 target = richardGo.transform.position + Vector3.up * 1.2f; // torso del sentado

            var explorer = (op != null && op.player != null) ? op.player.GetComponent<MapExplorer>() : null;
            if (explorer != null && explorer.enabled)
            {
                Vector3 to = target - cam.transform.position;
                Vector3 toFlat = to; toFlat.y = 0f;
                if (toFlat.sqrMagnitude > 1e-4f)
                    explorer.transform.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(toFlat.normalized).eulerAngles.y, 0f);
                float pitch = -Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                explorer.SetLookPitch(pitch);
                explorer.LockLook(2.4f, 3f);   // fija la mirada en el playero mientras dura el peek
            }

            float fov0 = cam.fieldOfView;
            yield return LerpFov(cam, fov0, fov0 - 22f, 0.4f);   // zoom in
            yield return new WaitForSeconds(1.2f);                // se queda viéndolo
            yield return LerpFov(cam, cam.fieldOfView, fov0, 0.4f); // vuelve
        }

        IEnumerator LerpFov(Camera cam, float from, float to, float dur)
        {
            float t = 0f;
            while (t < 1f && cam != null) { t += Time.deltaTime / Mathf.Max(0.01f, dur); cam.fieldOfView = Mathf.Lerp(from, to, Mathf.Clamp01(t)); yield return null; }
            if (cam != null) cam.fieldOfView = to;
        }

        // cámara ACTIVA (la que tiene el AudioListener prendido: persona o perro).
        Camera ActiveCamera()
        {
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.isActiveAndEnabled) { var c = l.GetComponent<Camera>(); if (c != null) return c; }
            return Camera.main;
        }

        // setea el color base del material (URP _BaseColor + fallback _Color). Con negro, la
        // textura se multiplica por 0 -> silueta negra; hacia blanco -> textura completa.
        static void SetMatColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        // reproduce un clip de Resources en un punto (si existe; si no, nada).
        static void PlayResource(string name, Vector3 at, float vol)
        {
            var clip = Resources.Load<AudioClip>(name);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, vol);
        }

        // ¿'p' está ADENTRO, pasado 'margin' metros del centro de la puerta (en la dirección de
        // avance screamApproachDir)? Usa la posición REAL de la puerta, no una línea fija afuera.
        bool PastDoor(SwingDoor door, Transform p, float margin)
        {
            if (door == null || p == null) return false;
            Vector3 c = DoorCenter(door);
            Vector3 n = screamApproachDir.sqrMagnitude > 1e-6f ? screamApproachDir.normalized : Vector3.forward;
            return Vector3.Dot(Flat(p.position) - Flat(c), Flat(n)) >= margin;
        }

        // ¿el jugador o el perro CRUZARON la línea (plano en 'point' perpendicular a 'dir', con
        // 'dir' apuntando hacia adentro)? Cruza = del lado positivo. Vale para toda la línea.
        static bool CrossedLine(Vector3 point, Vector3 dir, Transform a, Transform b)
        {
            Vector3 n = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            if (a != null && Vector3.Dot(Flat(a.position) - Flat(point), Flat(n)) >= 0f) return true;
            if (b != null && Vector3.Dot(Flat(b.position) - Flat(point), Flat(n)) >= 0f) return true;
            return false;
        }

        // distancia horizontal del más cercano (jugador o perro) a un punto.
        static float NearestDist(Vector3 point, Transform a, Transform b)
        {
            float best = float.MaxValue;
            if (a != null) best = Mathf.Min(best, Vector3.Distance(Flat(point), Flat(a.position)));
            if (b != null) best = Mathf.Min(best, Vector3.Distance(Flat(point), Flat(b.position)));
            return best;
        }

        // golpe en la puerta: el clip (Resources/door_knock) ya trae varios toques, así que
        // suena UNA sola vez al apretar E.
        IEnumerator Knock(Vector3 at)
        {
            var clip = Resources.Load<AudioClip>("door_knock");
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, 0.4f);
            yield return new WaitForSeconds(clip != null ? clip.length : 0.6f);
        }

        // Muestra una LÍNEA DE DIÁLOGO 'text' durante 'seconds' seg y la limpia. Helper para
        // agregar/editar diálogos con una sola línea: `yield return Say("...", 2f);`. Cuando
        // quieras más diálogos entre medio, se meten así, en el orden que va la escena.
        IEnumerator Say(string text, float seconds)
        {
            _hint = text;
            yield return new WaitForSeconds(seconds);
            _hint = "";
        }

        // owner: en los diálogos, la cámara del jugador APUNTA al que habla, un poco ZOOMEADA.
        // Le saca el control a MapExplorer un momento (solo si controlás la persona), aim + FOV, y
        // devuelve todo al terminar. 'target' = posición del que habla (le sumo altura de cara).
        IEnumerator FocusSay(string text, float seconds, Vector3 target)
        {
            var cam = ActiveCam();
            var uac = cam != null ? cam.GetComponent<Camera>() : null;
            float origFov = uac != null ? uac.fieldOfView : 60f;

            // BLOQUEO CINEMÁTICO: persona y perro dejan de mover cámara/caminar; la maneja esto.
            PartyController.CinematicLock = true;

            _hint = text;
            float t = 0f;
            Vector3 look = target + Vector3.up * 1.5f;   // a la cara del que habla
            while (t < seconds)
            {
                cam = ActiveCam();  // por si cambió
                if (cam != null)
                {
                    Vector3 dir = look - cam.position;
                    if (dir.sqrMagnitude > 1e-4f)
                        cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 16f * Time.deltaTime);
                    var c2 = cam.GetComponent<Camera>();
                    if (c2 != null) c2.fieldOfView = Mathf.Lerp(c2.fieldOfView, 40f, 10f * Time.deltaTime);   // zoom
                }
                t += Time.deltaTime;
                yield return null;
            }
            _hint = "";
            if (uac != null) uac.fieldOfView = origFov;   // restaurar FOV
            PartyController.CinematicLock = false;         // devolver el control
        }

        // cartel del guion (golpear / nadie responde / etc.)
        string _hint;
        void OnGUI()
        {
            if (string.IsNullOrEmpty(_hint)) return;
            // como estaba (sin fondo, blanco, centrado). Solo wordWrap + un poco más alto/ancho
            // para que los textos largos (los del viejo) no se corten.
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - 300f, Screen.height - 130f, 600f, 56f), _hint, style);
        }

        // teletransporta un NPC a 'pos' desactivando su CharacterController (no se puede
        // reasignar el transform de un CC activo).
        static void Teleport(Transform npc, Vector3 pos)
        {
            var cc = npc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            npc.position = pos;
            if (cc != null) cc.enabled = true;
        }

        // Camina un NPC hasta 'target' (se para a 'stopDist'), animando las piernas via
        // HumanWalkAnim (que detecta el movimiento por delta de posición). Sigue el piso con
        // un raycast hacia abajo. Movimiento simple (sin navmesh) -- suficiente para el guion.
        IEnumerator WalkTo(Transform npc, Vector3 target, float speed, float stopDist)
        {
            int guard = 0;
            while (npc != null && guard++ < 3000)
            {
                Vector3 pos = npc.position;
                Vector3 to = target - pos; to.y = 0f;
                if (to.magnitude <= stopDist) break;
                Vector3 dir = to.normalized;
                Vector3 next = pos + dir * speed * Time.deltaTime;
                // seguir el piso real, IGNORANDO sus propios colliders (si no, se subía a sí
                // misma) y eligiendo la superficie PISABLE MÁS ALTA que no sea un escalón
                // imposible (hasta stepUp por encima de los pies). Así pisa la tapa del cemento
                // (owner: "atraviesa los pies en el mini cemento") pero NO agarra el techo/
                // marquesina (queda muy por encima de la cabeza).
                const float stepUp = 0.45f;
                float groundY = pos.y;
                var hits = Physics.RaycastAll(next + Vector3.up * 3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
                float bestY = float.MinValue;
                foreach (var h in hits)
                {
                    if (h.collider.transform == npc || h.collider.transform.IsChildOf(npc)) continue;
                    if (h.point.y <= pos.y + stepUp && h.point.y > bestY) bestY = h.point.y;
                }
                if (bestY > float.MinValue) groundY = bestY;
                // pega los pies al piso directo (sin frenar el ajuste de Y -> no levita). El
                // fly-up viejo ya lo evitan la exclusión de sus propios colliders + elegir el
                // piso más cercano a los pies (ignora techos).
                next.y = groundY;
                npc.position = next;
                npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
                yield return null;
            }
        }
    }
}
