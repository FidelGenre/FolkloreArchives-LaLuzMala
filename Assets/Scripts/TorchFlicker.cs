// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TorchFlicker.cs — subtle Perlin-noise flicker for the player's
//  handheld light, so it reads as a torch/flame instead of a
//  steady electric flashlight beam.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    [RequireComponent(typeof(Light))]
    public class TorchFlicker : MonoBehaviour
    {
        public float baseIntensity = 9f;
        public float flickerAmount = 1.8f;
        public float flickerSpeed = 9f;

        Light torchLight;
        float seed;

        void Start()
        {
            torchLight = GetComponent<Light>();
            seed = Random.Range(0f, 1000f);
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
            torchLight.intensity = baseIntensity + (n - 0.5f) * 2f * flickerAmount;
        }
    }
}
