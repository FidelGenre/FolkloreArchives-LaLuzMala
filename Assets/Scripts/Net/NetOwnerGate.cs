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

            // El dueño VE su propio cuerpo (piernas al mirar abajo). Solo le colapsamos
            // la CABEZA (hueso "head") para no ver por dentro del cráneo al mirar al
            // frente. El compañero ve el modelo entero, con cabeza.
            if (mine)
            {
                var head = FindDeep(transform, "head");
                if (head != null) head.localScale = Vector3.one * 0.001f;
            }

            var explorer = GetComponent<MapExplorer>();
            if (explorer != null) explorer.enabled = mine;

            var dog = GetComponent<DogController>();
            if (dog != null)
            {
                dog.enabled = mine;
                if (mine) dog.mode = DogController.Mode.Player;  // en co-op el dueño lo maneja
            }

            var cc = GetComponent<CharacterController>();
            // el que NO es dueño deja que el NetworkTransform mueva el transform:
            // un CharacterController activo pelearía con las posiciones que llegan.
            if (cc != null) cc.enabled = mine;

            if (mine)
            {
                Cursor.lockState = CursorLockMode.Locked;
                // El DUEÑO se ubica en el piso. Con autoridad-del-dueño, si no lo hace,
                // su origen (0,0,0) queda bajo el terreno y el personaje cae al infinito.
                TeleportToGround(cc);
            }

            Debug.Log($"[NET] {name} spawn — IsOwner={mine} clientId={OwnerClientId} pos={transform.position}");
        }

        void TeleportToGround(CharacterController cc)
        {
            Vector3 p = new Vector3(408f + (OwnerClientId % 4) * 2f, 0f, 440f); // cerca del campamento
            var t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y + 0.3f;
            else p.y = 30f;
            // mover un CharacterController requiere desactivarlo un instante
            bool had = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            transform.position = p;
            if (cc != null) cc.enabled = had;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
