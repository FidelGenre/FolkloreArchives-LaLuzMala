// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  WindAmbience.cs — loop de viento de fondo (owner: "sonidos... viento").
//  3 clips (Free PSX Wind Ambience), uno elegido al azar por partida y
//  en loop -- no rota entre los 3 en vivo, es un lecho de ambiente fijo,
//  no un efecto puntual.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class WindAmbience : MonoBehaviour
    {
        public AudioClip[] clips;
        public float volume = 0.35f;
        [Range(0f, 1f)] public float spatialBlend = 0f; // 0 = 2D, envuelve todo el mapa

        void Start()
        {
            if (clips == null || clips.Length == 0) return;
            var src = gameObject.GetComponent<AudioSource>();
            if (src == null) src = gameObject.AddComponent<AudioSource>();
            src.clip = clips[Random.Range(0, clips.Length)];
            src.loop = true;
            src.volume = volume;
            src.spatialBlend = spatialBlend;
            src.playOnAwake = false;
            src.Play();
        }
    }
}
