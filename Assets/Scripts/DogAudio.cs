// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  DogAudio.cs — audio del perro. owner:
//   · Ladrido SOLO con botón (lo dispara DogController cuando lo controlás).
//     Un único sonido: el doble ladrido (Assets/Resources/dog_bark).
//   · Jadeo (dog_pant) en loop, sube al caminar.
//   · Pasos reusando el sistema del humano (surface-aware, más bajo/rápido).
//   · NADA de pasos ni jadeo-movido cuando el perro va SENTADO en el auto.
//  DogController se lo agrega solo.
// ============================================================
using UnityEngine;
using WASDSound;

namespace FolkloreArchives
{
    public class DogAudio : MonoBehaviour
    {
        public float barkVolume = 0.45f;  // ladrido (bajado, owner)
        public float pantVolume = 0.75f;  // jadeo/lengua (owner: "que suene un poco mas")
        public float stepWalk = 1.0f;     // caminando: pasos ESPACIADOS
        public float stepRun  = 0.6f;     // corriendo: trote
        public float runSpeed = 2.5f;     // m/s desde donde es "trote"
        public float footVolume = 0.22f;  // pasos del perro (bajo)

        AudioSource _src, _pant;
        WASDFootstepSource _foot;
        PlayerVehicleInteractor _veh;
        DogController _dog;
        AudioClip _bark;
        float _stepAccum, _lastStep;
        Vector3 _lastPos;

        void Awake()
        {
            _foot = GetComponent<WASDFootstepSource>();
            _veh  = GetComponent<PlayerVehicleInteractor>();
            _dog  = GetComponent<DogController>();

            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 1f; _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 2f; _src.maxDistance = 25f; _src.playOnAwake = false;
            _bark = Resources.Load<AudioClip>("dog_bark"); // el doble ladrido

            var pantClip = Resources.Load<AudioClip>("dog_pant");
            if (pantClip != null)
            {
                _pant = gameObject.AddComponent<AudioSource>();
                _pant.clip = pantClip; _pant.loop = true; _pant.playOnAwake = false;
                _pant.spatialBlend = 1f; _pant.rolloffMode = AudioRolloffMode.Linear;
                _pant.minDistance = 2f; _pant.maxDistance = 20f; _pant.volume = 0f;
                _pant.Play();
            }
            _lastPos = transform.position;
        }

        void Update()
        {
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            Vector3 delta = transform.position - _lastPos; _lastPos = transform.position;
            Vector3 hor = new Vector3(delta.x, 0f, delta.z);
            float sp = hor.magnitude / dt;

            // ¿va sentado en el auto? -> nada de pasos ni jadeo-movido
            bool inCar = _veh != null && _veh.CurrentSeat != null;

            if (_pant != null)
            {
                float mv = inCar ? 0f : Mathf.Clamp01(sp / 2f);
                float target = pantVolume * (0.25f + 0.75f * mv);
                _pant.volume = Mathf.Lerp(_pant.volume, target, 4f * dt);
            }

            if (inCar) { _stepAccum = 0f; return; }

            // en el aire (saltando) NO suenan pasos, aunque camines con W -> se pausan
            // hasta tocar el suelo.
            bool grounded = _dog == null || _dog.IsGrounded;
            if (!grounded) { _stepAccum = 0f; return; }

            // pasos: por DISTANCIA (espaciados) + tope de TIEMPO (no puede sonar más rápido
            // que cada minInt seg -> mata el spam aunque el perro vaya rápido/jittery).
            if (_foot != null && sp > 0.4f)
            {
                bool running = sp > runSpeed;
                float minInt = running ? 0.24f : 0.42f;
                _stepAccum += hor.magnitude;
                if (_stepAccum >= (running ? stepRun : stepWalk) && Time.time - _lastStep >= minInt)
                {
                    _stepAccum = 0f;
                    _lastStep = Time.time;
                    if (_foot.audioSource != null) _foot.audioSource.volume = footVolume;
                    _foot.PlayFootstepByAction(running ? WASDEnumAction.Run : WASDEnumAction.Walk,
                                               TerrainSurfaceDetector.At(transform.position, transform));
                }
            }
        }

        // salto del perro (lo llama DogController al saltar)
        public void PlayJump()
        {
            if (_foot == null) return;
            if (_foot.audioSource != null) _foot.audioSource.volume = footVolume;
            _foot.PlayFootstepByAction(WASDEnumAction.Jump, TerrainSurfaceDetector.At(transform.position, transform));
        }

        // Ladra (el doble ladrido). Lo llama DogController cuando el que controla al perro
        // aprieta el botón. NO hay ladridos automáticos.
        public void Bark()
        {
            if (_bark != null && _src != null) _src.PlayOneShot(_bark, barkVolume);
        }
    }
}
