// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  NetOwnerGate.cs — en un personaje en red, prende lo "local"
//  (cámara, AudioListener, control, cursor) SOLO para el dueño.
//  Los demás lo ven como un avatar sincronizado por el
//  OwnerNetworkTransform, sin cámara ni input.
//  1ª persona con conciencia del cuerpo: el dueño VE su cuerpo
//  (piernas al mirar abajo); solo se le oculta la cabeza.
// ============================================================
using Unity.Netcode;
using UnityEngine;

namespace FolkloreArchives.Net
{
    public class NetOwnerGate : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            bool mine = IsOwner;

            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.gameObject.SetActive(mine);   // cámara + AudioListener solo míos

            var explorer = GetComponent<MapExplorer>();
            if (explorer != null) explorer.enabled = mine;

            // owner: "no me deja interactuar... abrir puertas ni las opciones me salen"
            // -- subir/bajar del auto y abrir/cerrar puertas (persona en red).
            var interactor = GetComponent<PlayerVehicleInteractor>();
            if (interactor != null) interactor.enabled = mine;

            var dog = GetComponent<DogController>();
            if (dog != null)
            {
                dog.enabled = mine;
                if (mine) dog.mode = DogController.Mode.Player;  // en co-op el dueño lo maneja
            }

            // Conciencia del cuerpo (solo la PERSONA dueña): ve su torso/piernas al mirar
            // abajo; se le ocultan cabeza+cuello. El perro no usa esto (mira con el pitch
            // de DogController y su cámara va en el hocico).
            if (mine && dog == null)
            {
                var view = GetComponent<FirstPersonBodyView>();
                if (view == null) view = gameObject.AddComponent<FirstPersonBodyView>();
                view.cam = cam;
                view.Apply();
            }

            var cc = GetComponent<CharacterController>();
            // el que NO es dueño deja que el NetworkTransform mueva el transform:
            // un CharacterController activo pelearía con las posiciones que llegan.
            if (cc != null) cc.enabled = mine;

            if (mine)
            {
                Cursor.lockState = CursorLockMode.Locked;
                // El DUEÑO tiene que RE-AFIRMAR su posición. Con autoridad-del-dueño, el
                // NetworkTransform local del cliente puede arrancar en otro lado (ej.
                // 0,0,0, el transform original del prefab) aunque el SERVIDOR ya lo haya
                // instanciado bien -- si no hacemos este Teleport, el personaje cae al
                // infinito desde ese origen desincronizado.
                ReassertSpawnPosition(cc);
            }

            Debug.Log($"[NET] {name} spawn — IsOwner={mine} clientId={OwnerClientId} pos={transform.position}");
        }

        void ReassertSpawnPosition(CharacterController cc)
        {
            // owner: "necesito que el jugador 2 al unirse aparezca al lado del 1" --
            // NetGameSpawner (en el servidor) YA decide la posición correcta (al lado
            // de TEST_PLAYER o de un jugador ya conectado) y la usa para Instantiate.
            // Acá NO hay que recalcular nada de forma independiente: si buscáramos nuestro
            // propio "TEST_PLAYER" local, en Multiplayer Play Mode cada cliente virtual
            // corre en su PROPIO proceso con su PROPIO TEST_PLAYER (irrelevante al de los
            // demás) -- eso pisaría la posición "al lado del jugador 1" con la del
            // TEST_PLAYER local de ESTE cliente. Confío en transform.position, que ya
            // llegó sincronizado del servidor al spawnear -- solo reafirmo ESA posición
            // (fix de autoridad-del-dueño / interpolación), no invento una nueva.
            Vector3 p = transform.position;

            bool had = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            // owner: "al crear sala multiplayer aparezco volando cayendo en el mapa" --
            // un simple "transform.position = p" no le avisa al NetworkTransform que
            // esto es un TELEPORT: sigue interpolando desde su último estado conocido
            // hacia la posición real, y esa interpolación visible ES la "caída". Uso
            // Teleport() del propio componente para resetear su buffer de una.
            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null) nt.Teleport(p, transform.rotation, transform.localScale);
            else transform.position = p;

            if (cc != null) cc.enabled = had;
        }
    }
}
