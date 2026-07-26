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
            // owner: "necesito que aparezca donde este en el momento que lo toque" --
            // NetGameSpawner ya instancia el prefab en la posición correcta (la de
            // TEST_PLAYER si sigue activo, si no el campamento), pero con autoridad-del-
            // dueño el transform LOCAL del cliente puede no reflejar todavía esa
            // posición al momento de este callback. En vez de confiar en
            // transform.position (que es justo lo que podía estar desincronizado),
            // resuelvo el mismo origen que NetGameSpawner de forma independiente.
            var tp = GameObject.Find("TEST_PLAYER");
            Vector2 origin = (tp != null && tp.activeInHierarchy)
                ? new Vector2(tp.transform.position.x, tp.transform.position.z)
                : new Vector2(246f, 232f); // MapLayout.Campsite (runtime no puede ver MapLayout)

            Vector3 p = new Vector3(origin.x + (OwnerClientId % 4) * 2f, 0f, origin.y);
            var t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y + 0.3f;
            else p.y = 30f;

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
