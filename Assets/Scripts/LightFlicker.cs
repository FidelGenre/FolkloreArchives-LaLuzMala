// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  LightFlicker.cs — parpadeo sutil de una luz (foco viejo/vela)
//  para clima de terror. Varía la intensidad con ruido Perlin
//  (más orgánico que random puro) alrededor de su valor base.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public float amount = 0.35f;   // cuánto varía (fracción de la intensidad base)
        public float speed = 9f;       // velocidad del parpadeo
        [Range(0f, 1f)] public float dropChance = 0.015f; // prob. de "apagón" breve por frame

        Light _l;
        float _base, _seed, _dropUntil;

        void Start()
        {
            _l = GetComponent<Light>();
            _base = _l.intensity;
            _seed = Random.value * 100f;
        }

        void Update()
        {
            if (_l == null) return;
            // parpadeo suave con Perlin
            float n = Mathf.PerlinNoise(_seed, Time.time * speed) * 2f - 1f; // -1..1
            float mul = 1f + n * amount;
            // apagón breve ocasional (foco que "salta")
            if (Time.time < _dropUntil) mul *= 0.15f;
            else if (Random.value < dropChance) _dropUntil = Time.time + Random.Range(0.04f, 0.12f);
            _l.intensity = _base * Mathf.Max(0f, mul);
        }
    }
}
