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
            Material fallback = null;   // material URP creado en runtime, por si no hay ninguno bueno
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                // 1) buscar un material URP ya presente en este renderer (para conservar su textura)
                Material good = null;
                foreach (var m in mats)
                    if (IsUrp(m)) { good = m; break; }
                // 2) si no hay, crear uno URP — el shader URP siempre está incluido. Le
                //    paso la TEXTURA que ya trae el material roto del FBX (misma pinta que
                //    el personaje), y si no hay textura, un color piel.
                if (good == null)
                {
                    if (fallback == null)
                    {
                        var sh = Shader.Find("Universal Render Pipeline/Lit");
                        if (sh == null) return;   // ni URP hay: no puedo hacer nada
                        fallback = new Material(sh);
                        Texture tex = TexFromAny(mats);
                        if (tex != null && fallback.HasProperty("_BaseMap")) fallback.SetTexture("_BaseMap", tex);
                        else if (fallback.HasProperty("_BaseColor")) fallback.SetColor("_BaseColor", new Color(0.82f, 0.68f, 0.55f));
                        if (fallback.HasProperty("_Smoothness")) fallback.SetFloat("_Smoothness", 0.05f);
                    }
                    good = fallback;
                }
                // 3) reemplazar toda ranura que NO sea URP (magenta) por el material bueno
                bool changed = false;
                for (int k = 0; k < mats.Length; k++)
                    if (!IsUrp(mats[k])) { mats[k] = good; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static bool IsUrp(Material m) =>
            m != null && m.shader != null && m.shader.name.Contains("Universal");

        // saca la textura principal de cualquiera de los materiales (Standard usa _MainTex)
        static Texture TexFromAny(Material[] mats)
        {
            foreach (var m in mats)
            {
                if (m == null) continue;
                if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) return m.GetTexture("_MainTex");
                if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return m.GetTexture("_BaseMap");
            }
            return null;
        }
    }
}
