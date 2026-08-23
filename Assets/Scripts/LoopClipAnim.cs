using UnityEngine;

namespace FolkloreArchives
{
    // Reproduce un AnimationClip LEGACY en loop, con un offset de arranque para
    // DESINCRONIZAR varias copias (ej. las 4 gallinas, para que no piquen todas iguales).
    // Lo agrega ChickenCoopBuilder (editor) a cada animal de la granja. Corre en Play y
    // en el build. Usa el componente Animation (legacy) para no depender de un
    // AnimatorController por animal.
    [DisallowMultipleComponent]
    public class LoopClipAnim : MonoBehaviour
    {
        public AnimationClip clip;
        public float startOffset = 0f;   // segundos de desfasaje al arrancar
        public float speed = 1f;

        void Start()
        {
            if (clip == null) return;
            var a = GetComponent<Animation>();
            if (a == null) a = gameObject.AddComponent<Animation>();
            a.AddClip(clip, clip.name);
            a.clip = clip;
            a.wrapMode = WrapMode.Loop;
            a.Play(clip.name);
            var st = a[clip.name];
            if (st != null) { st.wrapMode = WrapMode.Loop; st.speed = speed; st.time = startOffset; }
        }
    }
}
