// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  FirstPersonBodyView.cs — conciencia del cuerpo en 1ª persona.
//  El DUEÑO ve su propio cuerpo (torso y piernas al mirar abajo);
//  solo se le ocultan CABEZA y CUELLO (colapsando esos huesos) para
//  no ver el interior del cráneo al mirar al frente. Y acerca el
//  near-clip de la cámara para que el torso, que está muy cerca, no
//  quede recortado. Lo usan la persona en red (dueño) y en solo.
//  El compañero NO lleva este componente → te ve con cabeza.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class FirstPersonBodyView : MonoBehaviour
    {
        // nombres candidatos del rig (Blender/Rigify: "head"/"neck"; por si acaso, mayúsculas)
        public string[] hideBones = { "head", "neck", "Head", "Neck", "spine.006" };
        public Camera cam;
        public float nearClip = 0.04f;   // por defecto 0.3 recortaría el torso, que está pegado a la cámara

        void Start() => Apply();

        public void Apply()
        {
            foreach (var n in hideBones)
            {
                var b = FindDeep(transform, n);
                if (b != null) b.localScale = Vector3.zero;   // triángulos degenerados → no se dibujan
            }
            if (cam == null) cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.nearClipPlane = nearClip;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
