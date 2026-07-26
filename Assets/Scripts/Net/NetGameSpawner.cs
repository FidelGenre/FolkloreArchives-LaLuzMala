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

        // spawn cerca del campamento (MapLayout.Campsite). Runtime no puede ver
        // MapLayout (es editor-only), así que va hardcodeado -- OJO: si el mapa
        // se vuelve a achicar/mover, hay que actualizar esto a mano (owner: "sigo
        // cayendo al infinito al tocar create host" -- el (408,440) viejo quedó
        // 27 unidades pasado el borde real del terreno, que hoy mide 413 en Z
        // (MapLayout.MapSize) desde que se achicó el mapa; ahí no hay collider,
        // caída infinita. Valor actual de MapLayout.Campsite: (246, 232)).
        static readonly Vector2 SpawnXZ = new Vector2(246f, 232f);

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
            // owner: consola tirando "NetworkPrefab (NetPerson) has a duplicate
            // GlobalObjectIdHash source entry" -- NetworkBuilder reconstruye el prefab
            // DE CERO (destruye y vuelve a crear el GameObject) en cada Generate y lo
            // regraba sobre el mismo .prefab; entre eso y jugar la escena muchas veces
            // en la misma sesión, la lista de prefabs de red del NetworkManager podía
            // terminar con una entrada vieja/duplicada para el mismo prefab. Saco
            // cualquier entrada previa para ESTE prefab antes de re-agregarlo, así
            // nunca hay más de una.
            if (_nm.NetworkConfig.Prefabs.Contains(p)) _nm.NetworkConfig.Prefabs.Remove(p);
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

        bool _personTaken, _dogTaken;
        readonly List<Transform> _spawned = new List<Transform>(); // jugadores ya spawneados, en orden

        void OnClientConnected(ulong clientId)
        {
            if (_nm == null || !_nm.IsServer) return;
            int choice = _choice.TryGetValue(clientId, out var c) ? c : 0;

            // resolver conflicto: si tu personaje ya está tomado, te toca el otro
            // (co-op de 2 → siempre uno persona + uno perro).
            if (choice == 0 && _personTaken) choice = 1;
            else if (choice == 1 && _dogTaken) choice = 0;

            var prefab = (choice == 1 && dogPrefab != null) ? dogPrefab : personPrefab;
            if (prefab == null) { Debug.LogError("[NET] Falta el prefab de personaje."); return; }
            if (choice == 1) _dogTaken = true; else _personTaken = true;

            // owner: "necesito que el jugador 2 al unirse aparezca al lado del 1" --
            // para el PRIMER jugador (normalmente el host) uso la posición de TEST_PLAYER
            // si sigue activo (todavía no lo apagó NetworkBootstrap.OnConnected). Para
            // cualquiera que se una DESPUÉS, TEST_PLAYER ya está apagado -- ahí uso la
            // posición ACTUAL del jugador de red ya conectado, así el 2do aparece al
            // lado del 1ro y no en el campamento. Si no hay nada de eso, cae al
            // campamento de siempre.
            Vector2 origin = SpawnXZ;
            var already = _spawned.Find(tr => tr != null);
            if (already != null) origin = new Vector2(already.position.x, already.position.z);
            else
            {
                var tp = GameObject.Find("TEST_PLAYER");
                if (tp != null && tp.activeInHierarchy) origin = new Vector2(tp.transform.position.x, tp.transform.position.z);
            }

            Vector3 pos = OnGround(new Vector3(origin.x + (clientId % 4) * 2f, 0f, origin.y));
            var go = Instantiate(prefab, pos, Quaternion.identity);
            _spawned.Add(go.transform);
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
