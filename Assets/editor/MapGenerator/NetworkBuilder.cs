// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  NetworkBuilder.cs — arma la infraestructura de red en la escena:
//   - root "NET" (persiste entre regenerados)
//   - NetworkManager + UnityTransport
//   - NetworkBootstrap (UI de conexión por código)
//   - prefab de jugador de prueba (cápsula) asignado como PlayerPrefab
//  Todo idempotente: se puede llamar en cada Generate sin duplicar.
// ============================================================
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class NetworkBuilder
    {
        const string CapsulePrefabPath = "Assets/_FolkloreArchives/Generated/NetCapsulePlayer.prefab";

        public static void EnsureNet()
        {
            var net = GameObject.Find("NET");
            if (net == null) net = new GameObject("NET");

            // panel de conexión (crea/une por código)
            if (net.GetComponent<FolkloreArchives.Net.NetworkBootstrap>() == null)
                net.AddComponent<FolkloreArchives.Net.NetworkBootstrap>();

            // NetworkManager + transporte
            var nm = net.GetComponent<NetworkManager>();
            if (nm == null) nm = net.AddComponent<NetworkManager>();
            var utp = net.GetComponent<UnityTransport>();
            if (utp == null) utp = net.AddComponent<UnityTransport>();

            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.PlayerPrefab = BuildCapsulePrefab();
            nm.NetworkConfig.ConnectionApproval = false;

            EditorUtility.SetDirty(nm);
        }

        // Prefab de jugador de prueba: cápsula + cámara 1ª persona (apagada, la prende
        // el dueño) + NetworkObject + OwnerNetworkTransform + NetPlayerSimple.
        static GameObject BuildCapsulePrefab()
        {
            var root = new GameObject("NetCapsulePlayer");
            root.AddComponent<NetworkObject>();
            root.AddComponent<FolkloreArchives.Net.OwnerNetworkTransform>();
            root.AddComponent<FolkloreArchives.Net.NetPlayerSimple>();

            var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.name = "Capsule";
            vis.transform.SetParent(root.transform);
            vis.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(vis.GetComponent<Collider>());

            var camGO = new GameObject("Camera");
            camGO.transform.SetParent(root.transform);
            camGO.transform.localPosition = new Vector3(0f, 3f, -5f);   // 3ª persona (ver la cápsula moverse)
            camGO.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            var cam = camGO.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.farClipPlane = MapLayout.CameraFarClip;
            camGO.AddComponent<AudioListener>();
            camGO.SetActive(false); // el dueño la prende en OnNetworkSpawn

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CapsulePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
