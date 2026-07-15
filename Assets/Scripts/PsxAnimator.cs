// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PsxAnimator.cs — hace que un Animator corra a FPS BAJO (estilo
//  PlayStation 1): pausa el auto-update y lo avanza en saltos, así
//  los movimientos se ven "cortados"/no interpolados, como PS1.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class PsxAnimator : MonoBehaviour
    {
        public float fps = 12f;   // cuadros por segundo de la animación (PS1 ~10-15)

        Animator anim;
        float accum;

        void Awake() { anim = GetComponentInChildren<Animator>(); }
        void OnEnable()  { if (anim != null) anim.enabled = false; } // apago el auto-update
        void OnDisable() { if (anim != null) anim.enabled = true; }

        void Update()
        {
            if (anim == null) return;
            accum += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, fps);
            if (accum >= step)
            {
                anim.Update(accum);   // avanza de golpe (paso grande = cortado)
                accum = 0f;
            }
        }
    }
}
