// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  FriendWander.cs — owner: "necesito ver como caminan" -- deambular
//  simple para los 3 amigos NPC (no son IA real todavía, solo para que
//  no estén parados fijos como estatuas): caminan de ida y vuelta entre
//  2 puntos cerca de donde arrancan, con una pausa en cada punta.
//  Se apoyan sobre el terreno real (Terrain.SampleHeight) en vez de
//  CharacterController/física -- son ambientación de fondo, no
//  necesitan colisión ni salto.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class FriendWander : MonoBehaviour
    {
        public float walkSpeed = 1.2f;
        public float turnSpeed = 120f;
        public float legRange = 2.5f;   // metros de ida y vuelta desde el punto de partida
        public float pauseTime = 1.5f;  // segundos parado en cada punta antes de girar
        public float arriveDistance = 0.15f;

        Vector3 _a, _b, _target;
        float _pauseUntil;

        void Start()
        {
            // arranca yendo hacia donde ya está mirando (el yaw que le puso FriendNpcBuilder)
            Vector3 fwd = transform.forward;
            _a = transform.position;
            _b = transform.position + fwd * legRange;
            _target = _b;
        }

        void Update()
        {
            if (Time.time < _pauseUntil) return;

            Vector3 to = _target - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < arriveDistance)
            {
                _target = (_target == _b) ? _a : _b;
                _pauseUntil = Time.time + pauseTime;
                return;
            }

            Quaternion lookRot = Quaternion.LookRotation(to.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRot, turnSpeed * Time.deltaTime);

            Vector3 newPos = transform.position + to.normalized * walkSpeed * Time.deltaTime;
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
                newPos.y = terrain.SampleHeight(newPos) + terrain.transform.position.y;
            transform.position = newPos;
        }
    }
}
