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

        void Start()
        {
            if (SkipForTesting)
            {
                Debug.Log("<color=yellow>[OpeningDriveSequence] SkipForTesting activo -- secuencia NO arranca, manejá a mano.</color>");
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

            // 1) sentar al jugador y al perro reales, sin mira/E.
            if (car.rearLeft != null) player.StartCoroutine(player.SitRoutine(car, car.rearLeft, null));
            if (car.rearMid != null) dog.StartCoroutine(dog.SitRoutine(car, car.rearMid, null));
            yield return new WaitForSeconds(player.enterDuration + 0.1f);

            // 2) recién ACÁ arranca el auto solo -- ya con jugador y perro sentados
            // del todo (glide de cámara terminado).
            if (autoDrive != null)
            {
                car.autoPilot = true;
                autoDrive.active = true;
                yield return new WaitUntil(() => autoDrive.HasArrived);
                // owner: "antes de frenar ya saltan los personajes del auto" --
                // HasArrived se prende apenas el auto está CERCA del último punto,
                // pero todavía puede tener velocidad/inercia (el throttle en 0 no para
                // en seco). Esperar a que la velocidad real baje antes de abrir puertas
                // y bajar a todos.
                yield return new WaitUntil(() => car.SpeedKmh < 2f);
            }

            // 3) frenar del todo (por las dudas) y abrir las 5 puertas.
            car.externalThrottle = 0f;
            car.externalSteer = 0f;
            car.autoPilot = false;
            if (carDoors != null && car.doors != null)
                foreach (var d in car.doors)
                    if (d != null) carDoors.SetDoor(d, true);

            player.StartCoroutine(player.ExitRoutine());
            dog.StartCoroutine(dog.ExitRoutine());
            yield return new WaitForSeconds(player.enterDuration + 0.1f);

            // 4) los 3 amigos decorativos quedan parados cerca del auto.
            StandFriend(friendMaleCasual, standMaleCasualLocal);
            StandFriend(friendMaleGreenJkt, standMaleGreenJktLocal);
            StandFriend(friendFemaleSec, standFemaleSecLocal);

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
