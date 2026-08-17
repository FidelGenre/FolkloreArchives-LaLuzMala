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
        // owner: "bajale muchisimo al viento" -- bajado bastante más de lo que
        // parece razonable a ojo porque un AudioSource 2D sin distancia/oclusión
        // se escucha más fuerte de lo esperado sobre todo el mapa.
        public float volume = 0.015f;
        [Range(0f, 1f)] public float spatialBlend = 0f; // 0 = 2D, envuelve todo el mapa

        AudioSource _src;

        void Start()
        {
            if (clips == null || clips.Length == 0) return;
            _src = gameObject.GetComponent<AudioSource>();
            if (_src == null) _src = gameObject.AddComponent<AudioSource>();
            _src.clip = clips[Random.Range(0, clips.Length)];
            _src.loop = true;
            _src.volume = volume;
            _src.spatialBlend = spatialBlend;
            _src.playOnAwake = false;
            _src.Play();
        }

        void Update()
        {
            if (_src == null) return;
            // owner: "adentro del auto tiene que dejar de sonar el viento" -- fundido suave
            // según si el personaje que controlás está sentado en el auto.
            float target = PlayerVehicleInteractor.LocalInCar ? 0f : volume;
            _src.volume = Mathf.Lerp(_src.volume, target, 4f * Time.deltaTime);
        }
    }
}
