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
            _done = true;
        }
    }
}
