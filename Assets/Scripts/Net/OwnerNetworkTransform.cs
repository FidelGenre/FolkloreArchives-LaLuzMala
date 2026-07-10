// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  OwnerNetworkTransform.cs — NetworkTransform con autoridad del
//  DUEÑO en vez del server. En co-op cada jugador mueve su propio
//  personaje y el anti-cheat no importa, así que el owner escribe
//  su transform y se sincroniza al resto.
// ============================================================
namespace FolkloreArchives.Net
{
    public class OwnerNetworkTransform : Unity.Netcode.Components.NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
