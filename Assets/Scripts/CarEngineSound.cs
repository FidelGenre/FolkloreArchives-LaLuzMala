// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarEngineSound.cs — ruido de motor. Usa Assets/Resources/engine_loop
//  si existe (si no, uno procedural). Para que NO suene "siempre el mismo
//  loop": DOS capas del mismo sample, una desfasada en el tiempo y
//  detuneada, cada una con su wobble propio -> nunca coinciden. El pitch
//  sube apenas con la velocidad; suena solo cuando el auto está encendido
//  (lo maneja el jugador o el autopilot). CarController se lo agrega solo.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    [RequireComponent(typeof(CarController))]
    public class CarEngineSound : MonoBehaviour
    {
        [Header("Motor")]
        public float baseFreq  = 70f;    // Hz del motor procedural (fallback)
        public float idlePitch = 0.95f;  // pitch en ralentí
        public float maxPitch  = 1.15f;  // pitch a máxima velocidad (rango angosto = no "acelera" todo el tiempo)
        public float volume    = 0.04f;  // volumen cuando está encendido (owner)
        public float refKmh    = 54f;    // km/h que se considera "a fondo"

        CarController car;
        AudioSource src, src2;

        void Awake()
        {
            car = GetComponent<CarController>();

            var clip = Resources.Load<AudioClip>("engine_loop");
            if (clip == null) clip = MakeEngineClip();

            src  = MakeSource(clip);
            src2 = MakeSource(clip);
            src.Play();
            src2.Play();
            // la 2da capa arranca en otra parte del loop -> los puntos de repetición
            // no coinciden nunca (con el detune de abajo, deja de sonar "el mismo loop").
            if (clip != null) src2.time = clip.length * 0.5f;
        }

        AudioSource MakeSource(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip         = clip;
            s.loop         = true;
            s.playOnAwake  = false;
            s.spatialBlend = 1f;      // 3D: sale del auto
            s.rolloffMode  = AudioRolloffMode.Linear;
            s.minDistance  = 3f;
            s.maxDistance  = 45f;
            s.volume       = 0f;      // arranca callado; sube al encenderse
            s.pitch        = idlePitch;
            return s;
        }

        void Update()
        {
            if (src == null || car == null) return;

            bool on = car.driving || car.autoPilot;
            float speed01   = Mathf.Clamp01(car.SpeedKmh / Mathf.Max(1f, refKmh));
            float basePitch = on ? Mathf.Lerp(idlePitch, maxPitch, speed01) : idlePitch;
            float targetVol = on ? volume : 0f;

            // capa 1
            float pWob1 = (Mathf.PerlinNoise(Time.time * 0.6f, 0.3f) - 0.5f) * 0.12f; // pitch ±0.06
            float vWob1 = (Mathf.PerlinNoise(Time.time * 0.9f, 1.7f) - 0.5f) * 0.40f; // volumen ±20%
            src.volume = Mathf.Lerp(src.volume, targetVol * (1f + vWob1), 4f  * Time.deltaTime);
            src.pitch  = Mathf.Lerp(src.pitch,  basePitch + pWob1,        1.5f * Time.deltaTime);

            // capa 2: detuneada + wobble de otra fase -> mata el "loop siempre igual"
            if (src2 != null)
            {
                float pWob2 = (Mathf.PerlinNoise(Time.time * 0.5f, 5.1f) - 0.5f) * 0.12f;
                float vWob2 = (Mathf.PerlinNoise(Time.time * 0.7f, 9.3f) - 0.5f) * 0.40f;
                src2.volume = Mathf.Lerp(src2.volume, targetVol * 0.7f * (1f + vWob2), 4f  * Time.deltaTime);
                src2.pitch  = Mathf.Lerp(src2.pitch,  basePitch * 1.055f + pWob2,      1.5f * Time.deltaTime); // más detune = menos "loop"
            }
        }

        // motor procedural (solo si falta Resources/engine_loop): armónicos graves + ruido.
        AudioClip MakeEngineClip()
        {
            const int sr = 44100;
            int len = sr;
            var data = new float[len];
            var rng = new System.Random(1234);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sr;
                float w = 0f;
                w += Mathf.Sin(2f * Mathf.PI * baseFreq * 0.5f * t) * 0.25f;
                w += Mathf.Sin(2f * Mathf.PI * baseFreq        * t) * 0.60f;
                w += Mathf.Sin(2f * Mathf.PI * baseFreq * 2f   * t) * 0.30f;
                w += Mathf.Sin(2f * Mathf.PI * baseFreq * 3f   * t) * 0.18f;
                w += ((float)rng.NextDouble() * 2f - 1f) * 0.12f;
                data[i] = Mathf.Clamp(w * 0.5f, -1f, 1f);
            }
            const int fade = 600;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[len - 1 - i] = Mathf.Lerp(data[len - 1 - i], data[i], 1f - k);
            }
            var clip = AudioClip.Create("EngineLoop", len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
