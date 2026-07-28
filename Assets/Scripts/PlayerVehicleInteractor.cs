// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PlayerVehicleInteractor.cs — subir/bajar del auto, MANUAL, con E.
//    Afuera, cerca de una PUERTA:  E abre / cierra esa puerta.
//    Afuera, PEGADO a un asiento (puerta abierta):  E te sienta.
//    Sentado con la puerta CERRADA:  E abre la puerta nomás (seguís sentado).
//    Sentado con la puerta ABIERTA, mirándola:  E la cierra (seguís sentado).
//    Conductor mirando al FRENTE (parabrisas):  E prende/apaga los faros
//      (misma acción que la tecla F de CarController, solo agrega el cartel).
//    Sentado con la puerta ABIERTA, mirando para otro lado:  E te baja.
//  Solo manejás desde el asiento del conductor. Mouse = free-look.
//  El estado de las puertas (abierta/cerrada) vive en CarDoors (Net/),
//  compartido entre todos los que miran el auto -- antes cada instancia
//  de este script llevaba su PROPIO estado, así que en red cada cliente
//  veía algo distinto (owner: "el perro ve que esta cerrada aunque la
//  persona ya la abrio").
//  canOpenDoors=false (el perro): puede sentarse si alguien más ya abrió
//  la puerta, y bajarse, pero no puede abrir/cerrar puertas él mismo
//  (owner: "necesito que el perro pueda subirse pero no abrir las
//  puertas").
// ============================================================
using System.Collections;
using FolkloreArchives.Net;
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
        public bool canOpenDoors = true; // false para el perro: se sienta pero no abre/cierra puertas

        CharacterController cc;
        MapExplorer explorer;
        DogController dog;
        HumanWalkAnim humanAnim;
        NetOwnerGate netGate;   // presente en red -- ahí vive la sincronización del achicado
        Transform cam, camParent;
        Camera camComp;
        Vector3 camLocalPos;

        // owner: "achicar el modelo del perro mientras esta sentado (como si se
        // acurrucara en el asiento)" -- un cuadrúpedo no tiene una pose "sentado en
        // silla humana" razonable a su tamaño normal; achicarlo mientras está en el
        // auto evita que atraviese techo/puertas.
        Transform ownModel;
        Vector3 dogModelScale = Vector3.one;
        const float DogSeatedScale = 0.45f; // owner: "las patas traseras atraviesan el asiento" -- un poco mas chico
        const float DogSeatedExtraDrop = 0.35f; // owner: "se ve volando mas alto de los asientos" -- hundirlo un poco mas
        const float DogSeatedForwardOffset = 0.1f; // owner: "ahora el perro esta muy adelantado" -- bajado de 0.3
        // owner: "el perro no deberia verse a si mismo" -- layer creado en Generate
        // (LayerSetup.cs) que la cámara EXCLUYE de su propio cullingMask mientras está
        // sentado. Es una propiedad LOCAL (layer + cullingMask), no sincronizada por
        // red -- cada cliente decide qué renderiza su PROPIA cámara, así que los demás
        // jugadores lo siguen viendo normal.
        const string SelfHiddenLayerName = "SelfHidden";
        int _prevModelLayer = -1;
        int _prevCullingMask;
        bool _selfHideActive;

        CarController car;      // null = a pie
        Transform mySeat, myDoor;
        CarInteractable currentTarget;   // lo que apunta la mira este frame
        bool busy;
        bool flashlightWasOn;   // estado de la linterna al subir (para restaurarlo al bajar)
        float lookYaw, lookPitch;

        // owner: "myDoor cambia entre L_Door_Back y L_Door_Front" -- la puerta
        // específica que YO abrí para entrar, recordada aparte de cualquier búsqueda
        // geométrica (que podía confundirse con OTRA puerta que hubiese quedado
        // abierta de una prueba anterior en el mismo auto).
        Transform _lastDoorOpened;
        CarController _lastDoorOpenedCar;

        static CarDoors Doors(CarController c) => c != null ? c.GetComponent<CarDoors>() : null;

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursive(child, layer);
        }

        void Start()
        {
            cc = GetComponent<CharacterController>();
            explorer = GetComponent<MapExplorer>();
            dog = GetComponent<DogController>();
            humanAnim = GetComponent<HumanWalkAnim>();
            netGate = GetComponent<NetOwnerGate>();
            var c = GetComponentInChildren<Camera>();
            if (c != null) { cam = c.transform; camComp = c; camParent = cam.parent; camLocalPos = cam.localPosition; }
            // ownModel: se usa tanto para el achicado del perro como para que
            // cualquiera (perro o persona) se oculte de su propia cámara al sentarse.
            ownModel = transform.Find("Model");
            if (dog != null && netGate == null && ownModel != null) // en red, NetOwnerGate ya lleva su propia base de escala
                dogModelScale = ownModel.localScale;
        }

        // owner: "cuando se une otro jugador ya no me aparecen las opciones de
        // interactuar" -- si el componente se llega a deshabilitar a mitad de una
        // corrutina (SitRoutine/SetDoor/ExitRoutine), Unity la corta de golpe SIN
        // llegar a la línea "busy = false" del final, dejando el flag trabado en true
        // para siempre -- eso bloquea Update() entero (ni el raycast de la mira se
        // vuelve a actualizar) y el "[E] ..." en pantalla. Este componente se
        // habilita/deshabilita seguido en red (NetOwnerGate lo prende/apaga según el
        // dueño), así que reseteo el flag cada vez que se reactiva, por las dudas.
        void OnEnable() { busy = false; }

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
                    var doors = Doors(car);
                    if (!canOpenDoors)
                    {
                        // el perro: no toca puertas, E siempre te baja (owner: "necesito
                        // que el perro pueda subirse pero no abrir las puertas")
                        StartCoroutine(ExitRoutine());
                    }
                    else if (LookingAtDoor(myDoor))
                    {
                        // mirando TU puerta → alterna abrir/cerrar, seguís sentado (owner:
                        // "apunto a la puerta y no me deja cerrarla"). Antes, con la puerta
                        // CERRADA, esto pasaba SIEMPRE sin importar hacia dónde miraras
                        // (owner: "mirando para fuera no me da la opcion de bajar" -- si tu
                        // puerta estaba cerrada, E solo la abría, nunca bajaba). Ahora el
                        // criterio es siempre el mismo: mirás tu puerta → la tocás; mirás
                        // para cualquier otro lado → bajás (ExitRoutine ya abre la puerta
                        // sola si hace falta, como parte de bajarte).
                        doors?.SetDoor(myDoor, !(doors != null && doors.IsOpen(myDoor)));
                    }
                    else if (car.driving && LookingForward(car))
                    {
                        // owner: "al mirar hacia delante estando en el auto deberia darme
                        // la opcion de prender y apagar las luces" -- mismo criterio que la
                        // puerta (mira + E), pero solo para el CONDUCTOR (los faros ya se
                        // podían prender con F; esto solo agrega el cartel/E mirando al
                        // frente, sin sacarle la tecla F que ya funcionaba).
                        car.SetHeadlights(!car.headlightsOn);
                    }
                    else
                    {
                        StartCoroutine(ExitRoutine());
                    }
                }
                else if (!canOpenDoors)
                {
                    // owner: "haz que el perro solo pueda sentarse en el medio" -- ya no
                    // alcanzaba con el fallback (solo actuaba si la mira NO encontraba
                    // nada apuntado); a veces igual apuntaba sin querer a otro asiento y
                    // terminaba ahí. Ahora el perro IGNORA la mira por completo: siempre
                    // va directo a rearMid con solo estar cerca del auto, sin importar a
                    // qué esté apuntando.
                    var nearCar = NearestCarInRange(doorRange);
                    if (nearCar != null && nearCar.rearMid != null)
                        StartCoroutine(SitRoutine(nearCar, nearCar.rearMid, PreferredDoor(nearCar, nearCar.rearMid.position)));
                }
                else if (target != null)   // a pie, apuntando algo del auto
                {
                    if (target.isSeat)
                        StartCoroutine(SitRoutine(target.car, target.part, PreferredDoor(target.car, target.part.position))); // apunto el asiento → subir
                    else
                    {
                        var doors = Doors(target.car);
                        bool willOpen = doors != null && !doors.IsOpen(target.part);
                        doors?.SetDoor(target.part, willOpen); // puerta → abrir/cerrar
                        // owner: "myDoor cambia entre L_Door_Back y L_Door_Front" -- me
                        // acuerdo de LA puerta específica que YO abrí, para no confundirla
                        // después con otra puerta cualquiera que haya quedado abierta de
                        // una prueba anterior en el mismo auto.
                        if (willOpen) { _lastDoorOpened = target.part; _lastDoorOpenedCar = target.car; }
                    }
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

        // Sentado manejando, "¿estoy mirando al frente (el parabrisas)?" -- mismo
        // criterio angular que LookingAtDoor, pero contra el forward del AUTO (no un
        // punto), para no pisarse con la puerta (que queda ~90° al costado).
        bool LookingForward(CarController c)
        {
            if (c == null || cam == null) return false;
            return Vector3.Angle(cam.forward, c.transform.forward) < 45f;
        }

        // MIRA invisible: qué parte del auto apunta el centro de la pantalla.
        // SphereCast (no un rayo de radio 0) porque sentado adentro, muy cerca de la
        // puerta, un rayo finito fallaba fácil por unos grados de diferencia -- eso
        // fue justo lo que causaba "quiero cerrar la puerta y me bajo" antes.
        // owner: "subo con el perro y lo sube al lado del male green, no en el medio" --
        // con los 3 asientos traseros pegados (Seat_RearMid entre RearL/RearR), elegir
        // por DISTANCIA a lo largo del barrido (quién toca primero) no es fiable: el
        // asiento vecino puede tocar la esfera antes aunque no sea al que apuntás. Ahora
        // se elige por ÁNGULO -- el CarInteractable cuyo anchor (ci.part.position) esté
        // más cerca del centro de la mira, sin importar cuál tocó primero.
        CarInteractable RaycastTarget()
        {
            if (cam == null) return null;
            var hits = Physics.SphereCastAll(cam.position, 0.15f, cam.forward, 4.5f, ~0, QueryTriggerInteraction.Collide);
            CarInteractable best = null; float bestAngle = float.MaxValue;
            foreach (var h in hits)
            {
                var ci = h.collider.GetComponentInParent<CarInteractable>();
                if (ci == null) continue;
                float angle = Vector3.Angle(cam.forward, ci.part.position - cam.position);
                if (angle < bestAngle) { bestAngle = angle; best = ci; }
            }
            return best;
        }

        static Transform[] Seats(CarController c) => new[] { c.driverSeat, c.frontPassenger, c.rearLeft, c.rearRight, c.rearMid };

        // el auto (por su transform raíz, no un asiento puntual) más cercano dentro de
        // 'range' -- usado por el perro para subir directo al asiento del medio sin
        // necesitar apuntar (ver Update).
        CarController NearestCarInRange(float range)
        {
            CarController best = null; float bd = range;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

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

        // owner: "myDoor cambia entre L_Door_Back y L_Door_Front" -- prioriza la puerta
        // que YO abrí específicamente (si sigue siendo del MISMO auto) por sobre
        // cualquier búsqueda geométrica de "la más cercana/abierta", que se confundía
        // con otras puertas abiertas por casualidad (de una prueba anterior, otro
        // jugador, etc.).
        Transform PreferredDoor(CarController c, Vector3 seatPos)
        {
            if (_lastDoorOpened != null && _lastDoorOpenedCar == c) return _lastDoorOpened;
            return NearestDoor(c, seatPos);
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
            var doors = Doors(c);
            Transform best = null; float bd = float.MaxValue;
            Transform bestOpen = null; float bdOpen = float.MaxValue;
            foreach (var d in c.doors)
            {
                if (d == null) continue;
                float dd = Vector3.Distance(d.position, to);
                if (dd < bd) { bd = dd; best = d; }
                if (doors != null && doors.IsOpen(d) && dd < bdOpen) { bdOpen = dd; bestOpen = d; }
            }
            return bestOpen != null ? bestOpen : best;
        }

        // asiento PEGADO (dentro de sitRange) cuya puerta esté ABIERTA
        (Transform, Transform, CarController) NearestOpenSeat()
        {
            Transform bs = null, bd = null; CarController bc = null; float best = sitRange;
            foreach (var c in Object.FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                var doors = Doors(c);
                foreach (var s in Seats(c))
                {
                    if (s == null) continue;
                    float d = Vector3.Distance(transform.position, s.position);
                    if (d < best)
                    {
                        Transform door = NearestDoor(c, s.position);
                        if (door != null && doors != null && doors.IsOpen(door)) { best = d; bs = s; bd = door; bc = c; }
                    }
                }
            }
            return (bs, bd, bc);
        }

        IEnumerator SitRoutine(CarController c, Transform seat, Transform door)
        {
            busy = true;
            if (explorer != null) explorer.enabled = false;
            // owner: "desde la perspectiva del perro no me deja mirar hacia los lados
            // dentro del auto" -- DogController tiene su PROPIA lógica de mouse-look
            // (gira transform y camT todos los frames en modo Player) que nunca se
            // enteraba de que estabas sentado, y peleaba con el mouse-look de acá mismo
            // por la rotación de la cámara. Se apaga igual que explorer.
            if (dog != null) dog.enabled = false;
            // owner: "achicar el modelo del perro mientras esta sentado" -- a tamaño
            // normal atravesaba techo/puertas (un cuadrúpedo no tiene una pose sentado
            // razonable ahí). Se restaura al bajar (ExitRoutine).
            // owner: "viendolo desde el humano no se achica" -- un simple localScale acá
            // es puramente LOCAL (no sincronizado); en red, NetOwnerGate lleva una
            // NetworkVariable para esto y lo replica a todos. Sin red (un jugador solo,
            // netGate null), se sigue achicando directo como antes.
            if (netGate != null) netGate.SetDogSeated(true);
            else if (ownModel != null) ownModel.localScale = dogModelScale * DogSeatedScale;

            // owner: "necesito que este en pose de conduccion... que lo sientes en la
            // silla como corresponde" -- sin esto la PERSONA se ve parada adentro del
            // auto. Mismo criterio de sincronización que el achicado del perro.
            if (netGate != null) netGate.SetPersonSeated(true);
            else if (humanAnim != null) humanAnim.seated = true;

            if (cc != null) cc.enabled = false;

            // owner: "desde la perspectiva del humano no veo que se haya subido al
            // auto" -- antes solo se movía la CÁMARA al asiento; el cuerpo (lo que ven
            // los DEMÁS clientes por el NetworkTransform de la raíz) se quedaba parado
            // afuera. Ahora el cuerpo también se teletransporta al asiento.
            // owner: "aparece asi arriba del auto y no sentado en el asiento" -- seat
            // es una posición de OJO (calculada desde el volante), no de PIES: poner la
            // raíz ahí directo dejaba al personaje parado a la altura del ojo, muy por
            // encima del asiento real. Corrijo restando la altura del ojo (camLocalPos.y)
            // así los PIES quedan a la altura del asiento.
            // owner (2): "aparece atravezado por fuera del auto" -- el perro tiene la
            // cámara pegada al HOCICO (offset hacia ADELANTE, no solo arriba); restar el
            // camLocalPos ENTERO empujaba el cuerpo hacia atrás y afuera del asiento.
            // Solo importa la altura acá -- la posición horizontal la pone seat.position
            // directo, sin tocar X/Z.
            // owner (3): "se ve volando mas alto de los asientos y no sentado" -- el
            // perro necesita hundirse un poco más en el asiento que lo que da la altura
            // de ojo sola (DogSeatedExtraDrop, solo para el perro).
            float extraDrop = dog != null ? DogSeatedExtraDrop : 0f;
            float forwardOffset = dog != null ? DogSeatedForwardOffset : 0f;
            transform.rotation = seat.rotation;
            transform.position = seat.position - transform.rotation * new Vector3(0f, camLocalPos.y + extraDrop, -forwardOffset);

            // owner: "quedo atras del asiento" -- ya no hace falta correr la cámara del
            // hocico hacia atrás: ahora el modelo del perro se OCULTA de su propia
            // cámara por layer (ver abajo), así que da igual si el hocico queda cerca o
            // adentro de la cámara -- vuelvo la cámara a offset cero, igual que la persona.
            yield return Glide(cam, seat.position, seat.rotation);
            cam.SetParent(seat, false);
            cam.localPosition = Vector3.zero; cam.localRotation = Quaternion.identity;
            lookYaw = 0f; lookPitch = 0f;

            // owner: "el perro no deberia verse a si mismo" / "me estoy viendo no
            // deberia verme cuando me subi" -- vale para los DOS: pone el modelo propio
            // en el layer SelfHidden y lo excluye del cullingMask de SU PROPIA cámara.
            // Layer + cullingMask son propiedades LOCALES (no sincronizadas por red):
            // los demás jugadores lo siguen viendo normal en su propia pantalla.
            if (ownModel != null && camComp != null)
            {
                int hidden = LayerMask.NameToLayer(SelfHiddenLayerName);
                if (hidden >= 0)
                {
                    _prevModelLayer = ownModel.gameObject.layer;
                    SetLayerRecursive(ownModel, hidden);
                    _prevCullingMask = camComp.cullingMask;
                    camComp.cullingMask &= ~(1 << hidden);
                    _selfHideActive = true;
                }
            }

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

            var exitDoors = Doors(c);
            if (door != null && exitDoors != null && !exitDoors.IsOpen(door)) exitDoors.SetDoor(door, true);

            // bajar JUSTO AL LADO DEL ASIENTO (no en el centro del auto), sobre el piso.
            // owner: "al bajar no baja bien" -- el 1.5f fijo se calibró para un auto
            // mucho más chico; con el Retro Car (6.6m) el jugador quedaba pisando el
            // collider del auto. Luego "sigo quedando delante de la puerta... deberia
            // bajarme delante del asiento" -- el intento anterior calculaba un punto
            // lateral desde el CENTRO del auto (mismo Z para cualquier asiento), así que
            // caía a la altura del centro del auto en vez de al lado de TU asiento. Ahora
            // trabajo en el espacio LOCAL del auto: conservo la posición a lo largo del
            // auto (Z local) del asiento, y solo empujo hacia afuera en el eje lateral (X
            // local) lo justo para despejar el ancho real del collider.
            var carCol = c.GetComponent<BoxCollider>();
            float halfWidth = carCol != null ? carCol.size.x * 0.5f : 1.0f;
            Vector3 seatLocal = c.transform.InverseTransformPoint(seat.position);
            float sign = seatLocal.x >= 0f ? 1f : -1f;
            Vector3 exitLocal = new Vector3(sign * (halfWidth + 0.8f), seatLocal.y, seatLocal.z);
            Vector3 side = c.transform.TransformPoint(exitLocal);
            side.y = GroundYIgnoring(c, side) + 0.05f;
            transform.position = side;
            // owner: "me sigue rotando la camara luego de haber bajado" (un salto de un
            // solo frame) -- el bug real: acá se forzaba SIEMPRE una pose neutra fija
            // (mirando derecho, nivel), descartando hacia dónde mirabas REALMENTE sentado
            // (lookYaw/lookPitch, el free-look del asiento). Si no mirabas justo al frente
            // del auto, el salto entre "cómo mirabas" y "la pose neutra forzada" se veía
            // como un giro de golpe. Ahora el cuerpo queda exactamente con el yaw que ya
            // tenía la cámara (auto.yaw + lookYaw) y la cámara vuelve a su pitch actual
            // (lookPitch) -- ni un grado de diferencia entre el último frame sentado y el
            // primero parado, cero salto.
            float exitYaw = c.transform.eulerAngles.y + lookYaw;
            float exitPitch = lookPitch;
            transform.rotation = Quaternion.Euler(0f, exitYaw, 0f);
            lookYaw = 0f; lookPitch = 0f;

            // deslizar la cámara del asiento hasta el ojo del jugador (SUAVE, camino corto).
            // Solo la POSICIÓN se anima -- la rotación se mantiene fija en la que ya tenía
            // sentado (sin girar durante la transición) y al reparentarla se restaura ESE
            // MISMO pitch (no uno neutro), así no hay ningún giro visible de por medio.
            Vector3 targetPos = camParent.TransformPoint(camLocalPos);
            yield return Glide(cam, targetPos, cam.rotation);
            cam.SetParent(camParent, false);
            cam.localPosition = camLocalPos;
            cam.localRotation = Quaternion.Euler(exitPitch, 0f, 0f);

            if (cc != null) cc.enabled = true;
            // MapExplorer/DogController guardan su propio "pitch" (privado) y lo
            // reaplican ni bien se reactivan -- sincronizado con el mismo exitPitch de
            // arriba, para que no haya ningún valor desactualizado que la pise al toque
            // (mismo criterio para los dos, sea cual sea el que tenga este personaje).
            if (explorer != null)
            {
                explorer.SetLookPitch(exitPitch);
                explorer.enabled = true;
            }
            // tamaño normal al bajar -- en red vía NetOwnerGate (sincronizado), sin red directo.
            if (netGate != null) netGate.SetDogSeated(false);
            else if (ownModel != null) ownModel.localScale = dogModelScale;
            // pose normal (de pie) al bajar -- mismo criterio de sincronización.
            if (netGate != null) netGate.SetPersonSeated(false);
            else if (humanAnim != null) humanAnim.seated = false;
            // restaurar el layer/cullingMask que ocultaban al personaje de su propia cámara.
            if (_selfHideActive)
            {
                if (ownModel != null) SetLayerRecursive(ownModel, _prevModelLayer);
                if (camComp != null) camComp.cullingMask = _prevCullingMask;
                _selfHideActive = false;
            }
            if (dog != null)
            {
                dog.SetLookPitch(exitPitch);
                dog.enabled = true;
            }
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

        void OnGUI()
        {
            if (busy) return;
            var target = currentTarget;
            string msg = null;
            if (car != null)
            {
                if (!canOpenDoors) msg = "[ E ] Bajar"; // el perro: nunca abre/cierra
                else if (LookingAtDoor(myDoor))
                {
                    // mismo criterio que la acción real de E: mirando TU puerta, alterna.
                    var doors = Doors(car);
                    msg = (doors != null && doors.IsOpen(myDoor)) ? "[ E ] Cerrar puerta" : "[ E ] Abrir puerta";
                }
                else if (car.driving && LookingForward(car))
                    msg = car.headlightsOn ? "[ E ] Apagar luces" : "[ E ] Prender luces";
                else msg = "[ E ] Bajar";
            }
            else if (!canOpenDoors)
            {
                // el perro solo puede sentarse en el medio -- mismo criterio que la
                // acción real de E, ignora la mira por completo.
                if (NearestCarInRange(doorRange) != null) msg = "[ E ] Subir";
            }
            else if (target != null)
            {
                if (target.isSeat) msg = "[ E ] Subir";
                else
                {
                    var doors = Doors(target.car);
                    msg = (doors != null && doors.IsOpen(target.part)) ? "[ E ] Cerrar puerta" : "[ E ] Abrir puerta";
                }
            }
            if (msg == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width / 2 - 140, Screen.height - 90, 280, 32), GUIContent.none);
            GUI.Label(new Rect(Screen.width / 2 - 140, Screen.height - 90, 280, 32), msg, style);
        }
    }
}
