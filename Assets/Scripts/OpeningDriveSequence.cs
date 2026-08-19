// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  OpeningDriveSequence.cs — owner: "vamos todos en el auto desde el
//  inicio de mapa hasta la gasolinera... al llegar a la gasolinera se
//  bajan todos... y el nuevo orden al subirse es persona 1 manejando y
//  perro de acompaniante y al subirse ambos, spawnean los otros 3 detras".
//
//  Orquesta TODA la secuencia de apertura, en un solo GameObject (el
//  auto, wireado por MapGenerator.cs al final de Generate):
//   1. Sienta al jugador y al perro REALES en rearLeft/rearMid, sin pasar
//      por la mira/tecla E (SitRoutine ahora es público, ver
//      PlayerVehicleInteractor.cs). Los 3 amigos decorativos ya están
//      sentados en conductor/rearRight/acompañante desde Generate
//      (FriendNpcBuilder.SeatInCar).
//   2. Espera a que CarAutoDrive llegue a la gasolinera (auto.HasArrived).
//   3. Frena, abre las 5 puertas, baja al jugador y al perro (ExitRoutine).
//   4. Los 3 amigos decorativos quedan parados cerca del auto (sin volver
//      a caminar -- FriendWander ya se destruyó permanentemente en Generate).
//   5. Marca PlayerVehicleInteractor.PastGasStation = true -- desde acá el
//      perro apunta al asiento de ACOMPAÑANTE en vez del medio (ver
//      PlayerVehicleInteractor.Update()).
//   6. Cuando el jugador maneja Y el perro es acompañante (los dos
//      sentados de verdad), los 3 amigos vuelven a aparecer sentados atrás.
//
//  Todos los números de posición de acá son una PRIMERA estimación --
//  como el resto de los asientos de esta sesión, van a necesitar ajuste
//  en vivo (Play) por el owner.
// ============================================================
using System.Collections;
using UnityEngine;

namespace FolkloreArchives
{
    public class OpeningDriveSequence : MonoBehaviour
    {
        [Header("Refs (las asigna MapGenerator.cs)")]
        public CarController car;
        public CarAutoDrive autoDrive;
        public FolkloreArchives.Net.CarDoors carDoors;
        public PlayerVehicleInteractor player; // TEST_PLAYER
        public PlayerVehicleInteractor dog;    // DOG
        public Transform friendMaleCasual, friendMaleGreenJkt, friendFemaleSec;

        [Header("Dónde quedan parados los 3 amigos al bajarse en la gasolinera (offset local al auto)")]
        public Vector3 standMaleCasualLocal   = new Vector3(-3f, 0f, 2f);
        public Vector3 standMaleGreenJktLocal = new Vector3(3f, 0f, 2f);
        public Vector3 standFemaleSecLocal    = new Vector3(0f, 0f, 3.5f);

        // owner: "necesito que el personaje aparezca parado ahí en esa posición mirando a
        // ese árbol, ya que estará meándolo". Posición/orientación FIJA EN EL MUNDO: el
        // owner movió el TEST_PLAYER a mano al pie del árbol y pasó estas coords del
        // Inspector. (Antes era un offset local al auto; ahora es un punto y una mirada
        // fijos hacia el árbol, independientes de dónde quede el auto.)
        public Vector3 standPlayerBeforeWorld = new Vector3(1863.246f, 16.64907f, 24.03101f);
        public float standPlayerBeforeWorldYaw = -16.712f; // grados MUNDO, mirando el árbol

        [Header("Dónde vuelven a sentarse atrás después de la gasolinera (local al auto, mismo criterio que FriendNpcBuilder)")]
        public Vector3 rearLeftLocal  = new Vector3(-0.620f, -0.1883f, -0.8f);
        public Vector3 rearMidLocal   = new Vector3(0f, -0.1883f, -0.75f);
        public Vector3 rearRightLocal = new Vector3(0.609f, -0.1883f, -0.7f);

        // owner: "no puedo manejar el auto para probar la ruta nueva, arranca la
        // secuencia sola" -- toggle de TESTING (Tools > Folklore Archives > Debug:
        // Saltar Secuencia Auto), mismo patrón que "Pasar a Día". En true, Play
        // arranca normal (jugador parado, sin auto-sentarse ni manejar solo) para
        // poder subirse y manejar a mano con WASD y anotar el trazado real.
        public static bool SkipForTesting = false;

        // owner: "botones para dar play y aparecer desde cierta escena, así no pruebo toda la
        // secuencia de 0". Checkpoint de DEBUG (lo setean botones del Scene View, ver
        // DebugCheckpointButtons.cs), guardado en EditorPrefs para que sobreviva al entrar a Play:
        //   0 = Meando (normal, todo desde el principio)
        //   1 = YPF bajada (saltea meado + viaje: aparece con todos bajándose en la gasolinera)
        //   2 = Tienda (además saltea la dispersión: chica en el baño y amigos ubicados, listo
        //       para golpear la oficina)
        public const string CheckpointKey = "FA_DebugCheckpoint";
        public static int ReadCheckpoint()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetInt(CheckpointKey, 0);
#else
            return 0;
#endif
        }

        void Start()
        {
            // owner: elegí un checkpoint (▶ YPF bajada) pero aparecía al lado del auto -> el
            // SkipForTesting ("manejar a mano") cortaba la secuencia antes. El checkpoint TIENE
            // PRIORIDAD: si elegiste uno (>=1), la secuencia corre igual, sin importar SkipForTesting.
            if (SkipForTesting && ReadCheckpoint() == 0)
            {
                Debug.Log("<color=yellow>[OpeningDriveSequence] SkipForTesting activo (y checkpoint 0) -- secuencia NO arranca, manejá a mano.</color>");
                return;
            }
            if (car == null || player == null || dog == null)
            {
                Debug.LogWarning($"OpeningDriveSequence: referencia sin conectar (car={car != null}, player={player != null}, dog={dog != null}) -- la secuencia no arranca.");
                return;
            }
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // owner: "no spawnie dentro del auto se fue sin mi" -- Start() de objetos
            // DISTINTOS (este script vive en el auto; player/dog en TEST_PLAYER/DOG) no
            // tiene orden garantizado en Unity. Esperar un frame acá asegura que el
            // PROPIO Start() de PlayerVehicleInteractor (que arma su referencia a la
            // cámara) ya corrió antes de llamar a SitRoutine.
            yield return null;

            // owner: "el perro no debería poder bajar antes de que se active el
            // script del auto" -- antes DrivingLocked recién se prendía DESPUÉS de
            // que el jugador terminara de subir, dejando una ventana (mientras el
            // jugador todavía camina hacia el auto) donde el perro ya está sentado
            // pero nada le impide bajarse con E. Prendido ACÁ, antes de sentar a
            // nadie -- nunca hay ventana sin traba.
            PlayerVehicleInteractor.DrivingLocked = true;

            int cp = ReadCheckpoint();
            if (cp >= 2)
            {
                // DEBUG (checkpoint 2): saltear TODA la YPF -> SOLO los 3 amigos ya sentados atrás.
                // Vos + Rufus quedan PARADOS al lado del auto: los subís vos (subir a Rufus y a la
                // persona al frente) y manejás al campamento. Sin secuencia de tienda.
                TeleportCarToYpf();
                yield return new WaitForSeconds(0.2f);
                ReseatFriend(friendMaleCasual, car.rearLeft, rearLeftLocal);
                ReseatFriend(friendMaleGreenJkt, car.rearMid, rearMidLocal);
                ReseatFriend(friendFemaleSec, car.rearRight, rearRightLocal);
                PlayerVehicleInteractor.PastGasStation = true;   // el perro apunta al asiento de acompañante
                // TODAS las puertas CERRADAS (los amigos ya están sentados). Vos abrís la de
                // adelante para subir (el interactor no te deja subir con la puerta cerrada).
                if (carDoors != null && car.doors != null)
                    foreach (var d in car.doors) if (d != null) carDoors.SetDoor(d, false);
                // vos + Rufus parados cerca del auto, mirándolo (los subís vos)
                PlaceStanding(player != null ? player.transform : null, new Vector3(459.73f, 17.08f, -44.16f), car.transform.position);
                PlaceStanding(dog    != null ? dog.transform    : null, new Vector3(458.07f, 17.08f, -36.77f), car.transform.position);
                PlayerVehicleInteractor.DrivingLocked = false;   // podés subir/bajar y manejar
                // (el capítulo campamento se re-conecta cuando el owner pase la coord del trigger)
                yield break;
            }
            if (cp >= 1)
            {
                // DEBUG (checkpoint YPF+): saltear el meado y el viaje -- sentar al perro y al
                // jugador directo y teletransportar el auto a la gasolinera. Después cae al
                // tramo COMÚN de abajo (bajar a todos + secuencia de tienda).
                if (car.rearMid != null) dog.StartCoroutine(dog.SitRoutine(car, car.rearMid, null));
                if (car.rearLeft != null) player.StartCoroutine(player.SitRoutine(car, car.rearLeft, null));
                // espera fija (no WaitUntil: se colgaba si uno no se sentaba, y quedabas en el
                // spawn). 1s alcanza para que termine el SitRoutine (glide ~0.6s) -- así no te
                // trabás en la puerta al bajar y el perro baja.
                yield return new WaitForSeconds(1f);
                TeleportCarToYpf();
                yield return new WaitForSeconds(0.35f);
            }
            else
            {
                // === NORMAL ===
                // 1) al perro lo sentamos directo (sin mira/E). Al jugador NO: aparece parado
                // meando, se le abre su puerta, y esperamos a que suba y la cierre con E.
                if (car.rearMid != null) dog.StartCoroutine(dog.SitRoutine(car, car.rearMid, null));

                Transform playerDoor = NearestDoorTo(car.rearLeft != null ? car.rearLeft.position : car.transform.position);
                StandPlayerBefore();
                if (carDoors != null && playerDoor != null) carDoors.SetDoor(playerDoor, true);

                PlayerVehicleInteractor.FrontSeatsBlocked = true;
                yield return new WaitUntil(() => player.CurrentSeat != null && !AnyDoorOpen());
                PlayerVehicleInteractor.FrontSeatsBlocked = false;

                // 2) recién ACÁ arranca el auto solo -- ya con el jugador sentado y su puerta cerrada.
                PlayerVehicleInteractor.DrivingLocked = true;
                if (autoDrive != null)
                {
                    car.autoPilot = true;
                    autoDrive.active = true;
                    yield return new WaitUntil(() => autoDrive.HasArrived);
                    yield return new WaitUntil(() => car.SpeedKmh < 2f);
                }
            }

            // 3) frenar del todo (por las dudas) y abrir las 5 puertas.
            PlayerVehicleInteractor.DrivingLocked = false;
            car.externalThrottle = 0f;
            car.externalSteer = 0f;
            car.autoPilot = false;
            if (carDoors != null && car.doors != null)
                foreach (var d in car.doors)
                    if (d != null) carDoors.SetDoor(d, true);

            player.StartCoroutine(player.ExitRoutine());
            dog.StartCoroutine(dog.ExitRoutine());
            yield return new WaitForSeconds(player.enterDuration + 0.1f);

            // owner: "ni bien bajan se cierran las puertas, MENOS la del jugador (esa la cierra él)".
            // El jugador venía en rearLeft -> su puerta = la más cercana a ese asiento; el resto se
            // cierran solas.
            Transform playerExitDoor = NearestDoorTo(car.rearLeft != null ? car.rearLeft.position : car.transform.position);
            if (carDoors != null && car.doors != null)
                foreach (var d in car.doors)
                    if (d != null && d != playerExitDoor) carDoors.SetDoor(d, false);

            // 4) los 3 amigos decorativos quedan parados cerca del auto.
            StandFriend(friendMaleCasual, standMaleCasualLocal);
            StandFriend(friendMaleGreenJkt, standMaleGreenJktLocal);
            StandFriend(friendFemaleSec, standFemaleSecLocal);

            // ETAPA "tienda YPF": ya bajaron todos en la gasolinera -> arranca la secuencia
            // del guion (la chica al baño, los 2 amigos quedan al lado del auto, vos + Rufus
            // van a la oficina, screamer, compras, hielo/ratas, grito del baño). Se construye
            // por etapas en YpfStorySequence. El re-sentado de abajo (pasos 5-6) queda para el
            // caso viejo de "seguir manejando"; durante el guion no se dispara (no manejás).
            gameObject.AddComponent<YpfStorySequence>().Begin(this);

            // 5) desde acá el perro apunta al asiento de acompañante, no al medio.
            PlayerVehicleInteractor.PastGasStation = true;

            // 6) cuando jugador+perro estén sentados de verdad (conductor+acompañante),
            // los 3 amigos vuelven a aparecer sentados atrás.
            yield return new WaitUntil(() =>
                player.CurrentSeat == car.driverSeat && dog.CurrentSeat == car.frontPassenger);

            ReseatFriend(friendMaleCasual, car.rearLeft, rearLeftLocal);
            ReseatFriend(friendMaleGreenJkt, car.rearMid, rearMidLocal);
            ReseatFriend(friendFemaleSec, car.rearRight, rearRightLocal);

            // owner: "cuando se suben todos de nuevo al auto las puertas traseras
            // deben cerrarse solas" -- las 5 puertas se abrieron TODAS en el paso 3
            // para que todos se bajaran; las traseras nunca se vuelven a cerrar porque
            // los 3 amigos se re-sientan con un teleport/reparent directo (ReseatFriend,
            // arriba), sin pasar por ninguna interacción de puerta. Cerrarlas acá a
            // mano, ya con todos adentro de verdad.
            if (carDoors != null && car.doors != null)
                foreach (var d in car.doors)
                    if (d != null) carDoors.SetDoor(d, false);
        }

        // ¿queda alguna puerta del auto abierta? -- ver el WaitUntil de arriba.
        bool AnyDoorOpen()
        {
            if (carDoors == null || car.doors == null) return false;
            foreach (var d in car.doors)
                if (d != null && carDoors.IsOpen(d)) return true;
            return false;
        }

        // la puerta de car.doors[] más cercana a un punto (ej. un asiento) -- mismo
        // criterio que PlayerVehicleInteractor.NearestDoor, pero acá solo hace falta
        // encontrarla, no elegir entre "más cercana" y "abierta".
        Transform NearestDoorTo(Vector3 pos)
        {
            if (car == null || car.doors == null) return null;
            Transform best = null; float bd = float.MaxValue;
            foreach (var d in car.doors)
            {
                if (d == null) continue;
                float dist = Vector3.Distance(d.position, pos);
                if (dist < bd) { bd = dist; best = d; }
            }
            return best;
        }

        // DEBUG: teletransporta el auto a la llegada de la YPF (último waypoint de la ruta del
        // auto-drive), con la orientación del último tramo, y frena. Para el checkpoint que
        // saltea el viaje. Los amigos/jugador/perro sentados viajan con el auto (van parentados).
        // owner: dónde estaciona el auto en la YPF (lo sacó del Renault12 tras un viaje normal).
        // INLINE (no campo público serializado) a propósito: si fuera serializado, la escena
        // guardaría el valor viejo (0,0,0) y pisaría este -- por eso quedaba en el fallback del
        // waypoint. Así siempre se usa el del código, sin tener que regenerar.
        static readonly Vector3 YpfCarParkPos = new Vector3(463.3693f, 16.99993f, -39.24857f);
        const float YpfCarParkYaw = -90.86f;

        void TeleportCarToYpf()
        {
            if (car == null) return;

            Quaternion rot = Quaternion.Euler(0f, YpfCarParkYaw, 0f);
            var rb = car.GetComponent<Rigidbody>();
            // solo si NO es kinematic (el auto es kinematic cuando no maneja -> setear velocidad
            // en un kinematic tira excepción y aborta la secuencia).
            if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            car.transform.SetPositionAndRotation(YpfCarParkPos, rot);
            // CLAVE: sincronizar también la posición del RIGIDBODY. En un rigidbody kinematic,
            // setear solo el transform se revierte (el rb tiene su posición física cacheada en el
            // spawn y lo devuelve) -> el auto "volvía" al pasto del spawn.
            if (rb != null) { rb.position = YpfCarParkPos; rb.rotation = rot; }
            car.externalThrottle = 0f; car.externalSteer = 0f; car.autoPilot = false;
        }

        // Para al jugador REAL en un punto FIJO del mundo (standPlayerBeforeWorld),
        // mirando el árbol (standPlayerBeforeWorldYaw), antes de que suba solo. El owner
        // ubicó el punto a mano moviendo el TEST_PLAYER en el Editor -- se usa tal cual
        // (misma técnica que el spawn horneado del auto en CarBuilder).
        // Para un personaje PARADO en 'pos' mirando 'faceTarget' (maneja el CharacterController,
        // que no deja reasignar el transform si está activo). Se usa en el checkpoint 2.
        static void PlaceStanding(Transform t, Vector3 pos, Vector3 faceTarget)
        {
            if (t == null) return;
            var cc = t.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            t.position = pos;
            Vector3 look = faceTarget - pos; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(look.normalized);
            if (cc != null) cc.enabled = true;
        }

        void StandPlayerBefore()
        {
            if (player == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; // no reasignar el transform de un CharacterController activo
            player.transform.position = standPlayerBeforeWorld;
            player.transform.rotation = Quaternion.Euler(0f, standPlayerBeforeWorldYaw, 0f);
            if (cc != null) cc.enabled = true;

            // owner: "al darle play no tenga prendida la linterna y esté mirando para abajo
            // así al chorro". Linterna OFF + cámara apuntada hacia abajo al arrancar (el
            // jugador después mira libre con el mouse; MapExplorer guarda/reaplica su pitch).
            var explorer = player.GetComponent<MapExplorer>();
            if (explorer != null)
            {
                explorer.SetFlashlight(false);
                explorer.SetLookPitch(42f); // grados hacia ABAJO (mirando el chorro)
                explorer.LockLook(3f, 12f); // 3 seg: solo mover ±12° alrededor del chorro, sin caminar
            }

            // owner: aparece MEANDO -- chorro procedural (PeeStream, sin assets). Se agrega
            // en runtime si el jugador no lo tiene, y se apaga solo apenas empieza a caminar
            // hacia el auto (ver PeeStream.Update).
            var pee = player.GetComponent<PeeStream>();
            if (pee == null) pee = player.gameObject.AddComponent<PeeStream>();
            pee.StartPee();
        }

        // desparenta al amigo del auto y lo deja parado (quieto, sin FriendWander --
        // ya se destruyó permanentemente en Generate) en un punto cerca del auto,
        // apoyado sobre el terreno real (mismo criterio que FriendWander.cs).
        void StandFriend(Transform friend, Vector3 localOffset)
        {
            if (friend == null || car == null) return;
            Vector3 pos = car.transform.TransformPoint(localOffset);
            var terrain = Terrain.activeTerrain;
            if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            // owner: "es como si no tuviera colision para los npcs el suelo de la
            // shell" -- en la YPF el piso real es una LOSA DE CEMENTO por encima del
            // terreno crudo (mesh aparte, ver AreaPoiBuilder/PlayonAsfalto). Samplear
            // SOLO el terreno los enterraba hasta los hombros bajo esa losa -- mismo
            // bug (y mismo fix) que ya tiene FriendNpcBuilder para el spawn inicial
            // cerca de la ruta pavimentada: quedarse con el piso MÁS ALTO de los dos
            // (terreno vs. cualquier collider real justo arriba, tipo la losa).
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = Mathf.Max(pos.y, hit.point.y);
            // owner: "no recuperaron su tamaño inicial" al bajarse. SetParent(null,true)
            // preserva la escala MUNDIAL, que hereda la del auto — si el auto no está en
            // escala 1 el amigo queda chico/grande. Lo desparentamos SIN preservar mundo y
            // forzamos escala 1 (el Model conserva su propia escala de altura): así el amigo
            // vuelve SIEMPRE a su tamaño de parado, sin importar la escala del auto.
            friend.SetParent(null, false);
            friend.localScale = Vector3.one;
            friend.position = pos;
            friend.rotation = Quaternion.Euler(0f, friend.eulerAngles.y, 0f); // parado derecho
            var anim = friend.GetComponent<HumanWalkAnim>();
            if (anim != null) anim.seated = false;
        }

        void ReseatFriend(Transform friend, Transform seat, Vector3 localPos)
        {
            if (friend == null || car == null) return;
            friend.SetParent(car.transform, false);
            friend.localRotation = Quaternion.identity;
            friend.localPosition = localPos;
            var anim = friend.GetComponent<HumanWalkAnim>();
            if (anim != null) anim.seated = true;
        }
    }
}
