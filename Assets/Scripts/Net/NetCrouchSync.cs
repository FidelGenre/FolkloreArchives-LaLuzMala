// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  NetCrouchSync.cs — replica por RED el estado de "agachado" del
//  personaje: el dueño lo escribe, todos lo leen, para que el
//  compañero te vea agacharte. Va SOLO en el prefab de red
//  (NetPerson). HumanWalkAnim lo lee si existe; en solo no está y
//  HumanWalkAnim usa el teclado local.
// ============================================================
using Unity.Netcode;

namespace FolkloreArchives
{
    public class NetCrouchSync : NetworkBehaviour
    {
        readonly NetworkVariable<bool> _crouch = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public bool IsOwnerLocal => IsSpawned && IsOwner;
        public bool Crouched => IsSpawned && _crouch.Value;

        public void SetLocal(bool v)
        {
            if (IsSpawned && IsOwner && v != _crouch.Value) _crouch.Value = v;
        }
    }
}
