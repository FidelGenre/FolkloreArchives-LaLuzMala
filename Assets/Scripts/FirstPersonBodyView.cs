// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  FirstPersonBodyView.cs — conciencia del cuerpo en 1ª persona.
//  El DUEÑO ve su propio cuerpo (torso y piernas al mirar abajo).
//  NO oculta huesos: escalar un hueso de un skinned mesh a ~0
//  corrompe el skinning (la malla se rompe/magenta). En su lugar,
//  baja la cámara del tope del cráneo a la altura de los OJOS y la
//  saca un poco hacia adelante (fuera de la cara), de modo que la
//  cabeza queda DETRÁS de la cámara y no se ve por dentro; al mirar
//  abajo, se ve el cuerpo. Acerca el near-clip para no recortar el
//  torso, que queda muy cerca. Lo usan persona en red (dueño) y solo.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class FirstPersonBodyView : MonoBehaviour
    {
        public Camera cam;
        public float nearClip = 0.04f;
        public float eyeDrop = 0.15f;      // del tope del cráneo a los ojos
        public float eyeForward = 0.12f;   // saca la cámara por delante de la cara (+Z = adelante)

        bool _done;

        void Start() => Apply();

        public void Apply()
        {
            if (_done) return;
            if (cam == null) cam = GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.nearClipPlane = nearClip;
                var lp = cam.transform.localPosition;
                cam.transform.localPosition = new Vector3(lp.x, lp.y - eyeDrop, lp.z + eyeForward);
            }
            FixMagentaMaterials();
            _done = true;
        }

        // Repara en runtime las sub-mallas magenta: si el modelo tiene ranuras con el
        // material "Standard" del FBX (magenta en URP), las reemplaza por el material
        // URP bueno que ya tiene el propio modelo. Es un parche para tu vista sin tener
        // que regenerar; el fix permanente (para el compañero también) es regenerar.
        void FixMagentaMaterials()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                Material good = null;
                foreach (var m in mats)
                    if (m != null && m.shader != null && m.shader.name.Contains("Universal")) { good = m; break; }
                if (good == null) continue;   // ninguna ranura URP: no puedo saber cuál es la buena
                bool changed = false;
                for (int k = 0; k < mats.Length; k++)
                    if (mats[k] == null || mats[k].shader == null || !mats[k].shader.name.Contains("Universal"))
                    { mats[k] = good; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
