// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  NetOwnerGate.cs — en un personaje en red, prende lo "local"
//  (cámara, AudioListener, control, cursor) SOLO para el dueño.
//  Los demás lo ven como un avatar sincronizado por el
//  OwnerNetworkTransform, sin cámara ni input.
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

            var cc = GetComponent<CharacterController>();
            // el que NO es dueño deja que el NetworkTransform mueva el transform:
            // un CharacterController activo pelearía con las posiciones que llegan.
            if (cc != null) cc.enabled = mine;

            if (mine) Cursor.lockState = CursorLockMode.Locked;

            Debug.Log($"[NET] {name} spawn — IsOwner={mine} clientId={OwnerClientId}");
        }
    }
}
