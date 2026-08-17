// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarDoors.cs — estado de las puertas del auto, COMPARTIDO entre
//  todos los que lo miran. Antes cada PlayerVehicleInteractor llevaba
//  su PROPIO estado de puertas (un HashSet local) -- andaba bien con
//  un solo observador, pero en red cada cliente veía algo distinto
//  (owner: "el perro ve que esta cerrada aunque la persona ya la
//  abrio"). Funciona igual sin NetworkManager corriendo (animación
//  puramente local, un solo jugador) y en red (sincronizado por
//  NetworkVariable, cambios pedidos via ServerRpc).
// ============================================================
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FolkloreArchives.Net
{
    public class CarDoors : NetworkBehaviour
    {
        public FolkloreArchives.CarController car;   // set por CarBuilder al construir el auto
        public float doorOpenDeg = 72f;
        public float doorVolume = 0.7f;              // sonido puerta auto (owner: qubodup 147244)

        // owner: "ruido de puerta de auto" -- clips recortados (abrir / portazo) de
        // Assets/Resources. Se reproducen 3D en la posición de la puerta desde
        // AnimateDoor, el único punto por donde pasa CUALQUIER apertura/cierre (jugador,
        // perro o la secuencia de apertura), y que corre en todos los clientes.
        AudioClip _openClip, _closeClip;
        void Awake()
        {
            _openClip  = Resources.Load<AudioClip>("car_door_open");
            _closeClip = Resources.Load<AudioClip>("car_door_close");
        }

        readonly HashSet<Transform> _localOpen = new HashSet<Transform>();          // fallback un solo jugador
        readonly Dictionary<Transform, Quaternion> _closedRot = new Dictionary<Transform, Quaternion>();
        readonly NetworkVariable<int> _openMask = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        static bool Networked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        public override void OnNetworkSpawn()
        {
            _openMask.OnValueChanged += OnMaskChanged;
            ApplyMaskInstant(_openMask.Value); // por si te uniste con puertas ya abiertas
        }

        public override void OnNetworkDespawn()
        {
            _openMask.OnValueChanged -= OnMaskChanged;
        }

        public bool IsOpen(Transform door)
        {
            if (door == null) return false;
            if (Networked)
            {
                int i = DoorIndex(door);
                return i >= 0 && (_openMask.Value & (1 << i)) != 0;
            }
            return _localOpen.Contains(door);
        }

        public void SetDoor(Transform door, bool open)
        {
            if (door == null) return;
            if (Networked)
            {
                int i = DoorIndex(door);
                if (i < 0) return;
                if (IsServer) SetMaskBit(i, open);
                else SetDoorServerRpc(i, open);
            }
            else
            {
                StartCoroutine(AnimateDoor(door, open));
                if (open) _localOpen.Add(door); else _localOpen.Remove(door);
            }
        }

        int DoorIndex(Transform door)
        {
            if (car == null || car.doors == null) return -1;
            return System.Array.IndexOf(car.doors, door);
        }

        [ServerRpc(RequireOwnership = false)]
        void SetDoorServerRpc(int doorIndex, bool open) => SetMaskBit(doorIndex, open);

        void SetMaskBit(int i, bool open)
        {
            int m = _openMask.Value;
            _openMask.Value = open ? (m | (1 << i)) : (m & ~(1 << i));
        }

        void OnMaskChanged(int prev, int now)
        {
            if (car == null || car.doors == null) return;
            for (int i = 0; i < car.doors.Length; i++)
            {
                bool was = (prev & (1 << i)) != 0, isNow = (now & (1 << i)) != 0;
                if (was != isNow && car.doors[i] != null) StartCoroutine(AnimateDoor(car.doors[i], isNow));
            }
        }

        void ApplyMaskInstant(int mask)
        {
            if (car == null || car.doors == null) return;
            for (int i = 0; i < car.doors.Length; i++)
            {
                var door = car.doors[i];
                if (door == null) continue;
                if (!_closedRot.ContainsKey(door)) _closedRot[door] = door.localRotation;
                bool open = (mask & (1 << i)) != 0;
                if (open) door.localRotation = OpenRotation(door);
            }
        }

        Quaternion OpenRotation(Transform door)
        {
            Quaternion closed = _closedRot[door];
            float sign = car.transform.InverseTransformPoint(door.position).x < 0f ? 1f : -1f;
            // Bisagra en el eje vertical del MUNDO, no el eje Y "local" del nodo (mismo
            // criterio que el AnimateDoor original en PlayerVehicleInteractor).
            Vector3 hingeAxis = door.parent.InverseTransformDirection(Vector3.up);
            return Quaternion.AngleAxis(sign * doorOpenDeg, hingeAxis) * closed;
        }

        IEnumerator AnimateDoor(Transform door, bool open)
        {
            if (!_closedRot.ContainsKey(door)) _closedRot[door] = door.localRotation;
            // sonido de puerta de auto (3D, en la puerta) -- abrir vs portazo
            var clip = open ? _openClip : _closeClip;
            if (clip != null) AudioSource.PlayClipAtPoint(clip, door.position, doorVolume);
            Quaternion closed = _closedRot[door];
            Quaternion openRot = OpenRotation(door);
            Quaternion from = door.localRotation, to = open ? openRot : closed;
            float t = 0f;
            const float dur = 0.35f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                door.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            door.localRotation = to;
        }
    }
}
