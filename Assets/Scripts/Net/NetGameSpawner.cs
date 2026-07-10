// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  NetGameSpawner.cs — en el servidor, spawnea el personaje que
//  cada jugador ELIGIÓ (persona o perro). La elección llega como
//  1 byte en la ConnectionData (0=persona, 1=perro), leído en el
//  callback de Connection Approval. Componente en el objeto NET;
//  las refs a los prefabs las setea NetworkBuilder.
// ============================================================
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FolkloreArchives.Net
{
    public class NetGameSpawner : MonoBehaviour
    {
        public GameObject personPrefab;
        public GameObject dogPrefab;

        // spawn cerca del campamento (MapLayout.Campsite ≈ 410,442). Runtime no puede
        // ver MapLayout (es editor-only), así que va hardcodeado.
        static readonly Vector2 SpawnXZ = new Vector2(408f, 440f);

        readonly Dictionary<ulong, int> _choice = new Dictionary<ulong, int>(); // clientId → 0 persona / 1 perro
        NetworkManager _nm;

        void Start()
        {
            _nm = NetworkManager.Singleton;
            if (_nm == null) { Debug.LogError("[NET] NetGameSpawner: no hay NetworkManager."); return; }

            // registrar los prefabs ANTES de conectar (deben estar en la lista de red)
            TryAddPrefab(personPrefab);
            TryAddPrefab(dogPrefab);

            _nm.ConnectionApprovalCallback = Approve;
            _nm.OnClientConnectedCallback += OnClientConnected;
        }

        void TryAddPrefab(GameObject p)
        {
            if (p == null) return;
            try { _nm.AddNetworkPrefab(p); } catch { /* ya estaba registrado */ }
        }

        void OnDestroy()
        {
            if (_nm != null) _nm.OnClientConnectedCallback -= OnClientConnected;
        }

        // corre en el SERVIDOR por cada cliente que intenta conectarse
        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse resp)
        {
            int choice = (req.Payload != null && req.Payload.Length > 0) ? req.Payload[0] : 0;
            _choice[req.ClientNetworkId] = choice;
            resp.Approved = true;
            resp.CreatePlayerObject = false;   // lo spawneamos nosotros (por elección) en OnClientConnected
        }

        void OnClientConnected(ulong clientId)
        {
            if (_nm == null || !_nm.IsServer) return;
            int choice = _choice.TryGetValue(clientId, out var c) ? c : 0;
            var prefab = (choice == 1 && dogPrefab != null) ? dogPrefab : personPrefab;
            if (prefab == null) { Debug.LogError("[NET] Falta el prefab de personaje."); return; }

            Vector3 pos = OnGround(new Vector3(SpawnXZ.x + (clientId % 4) * 2f, 0f, SpawnXZ.y));
            var go = Instantiate(prefab, pos, Quaternion.identity);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
            Debug.Log($"[NET] spawn {(choice == 1 ? "PERRO" : "PERSONA")} para cliente {clientId} en {pos}");
        }

        static Vector3 OnGround(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y + 0.2f;
            return p;
        }
    }
}
