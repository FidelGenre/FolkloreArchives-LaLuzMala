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

        // owner: "necesito que el personaje principal al tocar play aparezca parado al
        // lado del auto y la puerta de atrás abierta así está meando y cuando se sube y
        // cierra la puerta arranca el auto" -- offset local al auto, afuera de la
        // puerta trasera del jugador (rearLeft), mirando hacia el costado (no hacia el
        // auto). owner: "no está apareciendo el pj al lado del auto" -- el offset
        // original (-2.5,0,-1.2) cayó AFUERA del asfalto (el auto spawnea en la punta
        // misma de la ruta, donde el mapa se corta) y el jugador terminó cayendo al
        // vacío (Y=-43 confirmado en el Inspector). Acercado bien al auto (sin
        // desplazamiento adelante/atrás, solo al costado) para pisar la misma malla
        // que el auto. Primera estimación, como todo lo demás de esta escena -- ajustar
        // en vivo.
        public Vector3 standPlayerBeforeLocal = new Vector3(-2f, 0f, 0f);
        public float standPlayerBeforeYaw = -90f; // grados locales al auto, hacia dónde mira parado

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

            // 1) al perro lo sentamos directo (sin mira/E, como antes). Al jugador
            // NO -- owner: "necesito que aparezca parado al lado del auto y la puerta
            // de atrás abierta así está meando, y cuando se sube y cierra la puerta
            // arranca el auto". Se lo para afuera, se le abre SU puerta (rearLeft), y
            // se espera a que él mismo se siente y la cierre con E (misma interacción
            // manual que cualquier puerta del juego) antes de arrancar.
            if (car.rearMid != null) dog.StartCoroutine(dog.SitRoutine(car, car.rearMid, null));

            Transform playerDoor = NearestDoorTo(car.rearLeft != null ? car.rearLeft.position : car.transform.position);
            StandPlayerBefore();
            if (carDoors != null && playerDoor != null) carDoors.SetDoor(playerDoor, true);

            // owner: "me subí adelante... no debería pasar, debería solo poder subir
            // atrás" -- los asientos de adelante ya los ocupan los amigos decorativos
            // (FriendNpcBuilder.SeatInCar, en Generate); sin esto la mira igual los
            // ofrecía y el jugador se teletransportaba encima de un amigo.
            PlayerVehicleInteractor.FrontSeatsBlocked = true;

            // owner: "se está sentando arriba del perro... al cerrar la puerta no
            // está arrancando" -- el banco trasero tiene los 3 asientos pegados
            // (rearLeft/rearMid/rearRight), y la mira (RaycastTarget en
            // PlayerVehicleInteractor) no sabe ni le importa qué puerta abrimos --
            // elige el asiento más cerca del centro de pantalla sin importar la
            // puerta, así que era fácil terminar sentado en rearMid (el del perro,
            // justo al lado) en vez de rearLeft. Esperar específicamente
            // "CurrentSeat == car.rearLeft" nunca se cumplía en ese caso. No importa
            // en qué asiento trasero termine sentándose: lo que de verdad marca "ya
            // subió y cerró todo" es estar sentado EN ALGÚN LADO y que no quede
            // ninguna puerta del auto abierta.
            yield return new WaitUntil(() => player.CurrentSeat != null && !AnyDoorOpen());
            PlayerVehicleInteractor.FrontSeatsBlocked = false; // ya subió atrás -- para el próximo tramo (leg 2) sí puede elegir el volante

            // 2) recién ACÁ arranca el auto solo -- ya con el jugador sentado de
            // verdad y su puerta cerrada.
            // owner: "necesito que no dé opciones a abrir la puerta ni bajar a los
            // personajes hasta llegar a la gasolinera y frenar" -- nadie (jugador NI
            // perro) puede tocar puertas o bajarse mientras el auto maneja solo.
            // Se destraba recién en el paso 3, ya frenado del todo, ANTES de abrir
            // las puertas para que todos bajen.
            PlayerVehicleInteractor.DrivingLocked = true;
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

        // Para al jugador REAL afuera del auto, al lado de su puerta (standPlayerBeforeLocal),
        // antes de que suba solo. Mismo criterio de piso que StandFriend (terreno vs.
        // cualquier collider real más arriba, ej. una losa de cemento).
        void StandPlayerBefore()
        {
            if (player == null || car == null) return;
            Vector3 pos = car.transform.TransformPoint(standPlayerBeforeLocal);
            // owner: "no está apareciendo el pj al lado del auto" -- cayó al vacío
            // (Y=-43 en el Inspector). El auto spawnea en la punta misma de la ruta
            // (donde el mapa se corta), así que cualquier offset que se pase de la
            // malla real cae afuera de todo. Red de seguridad: si NINGUNA de las dos
            // fuentes (terreno, raycast) encuentra algo cerca de la altura del auto
            // (±3m es más que suficiente para cualquier vereda/cordón real), usar la
            // altura del propio auto en vez de lo que haya dado el sampleo -- nunca
            // cae al vacío, en el peor caso aparece a la altura del auto en vez de
            // pisando el suelo exacto.
            float carY = car.transform.position.y;
            float groundY = carY;
            bool found = false;
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float sampled = terrain.SampleHeight(pos) + terrain.transform.position.y;
                if (Mathf.Abs(sampled - carY) <= 3f) { groundY = sampled; found = true; }
            }
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(hit.point.y - carY) <= 3f)
            {
                groundY = found ? Mathf.Max(groundY, hit.point.y) : hit.point.y;
                found = true;
            }
            pos.y = found ? groundY : carY;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; // no reasignar el transform de un CharacterController activo
            player.transform.position = pos;
            player.transform.rotation = car.transform.rotation * Quaternion.Euler(0f, standPlayerBeforeYaw, 0f);
            if (cc != null) cc.enabled = true;
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
