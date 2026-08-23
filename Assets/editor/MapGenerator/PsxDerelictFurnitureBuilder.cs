// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PsxDerelictFurnitureBuilder.cs — owner: "añadime este pack a la casa de la vieja
//  pero APARTE de los que ya están". Asset: "PSX Derelict Furniture" by Daniel Jurys
//  (itch.io) — ver ASSET_CREDITS.md. 6 GLB sueltos (silla, sofá, heladera, colchón,
//  estante, jarrón), muebles ABANDONADOS. Ya vienen a tamaño real (escala 1) y sin
//  animación.
//
//  Se colocan AGRUPADOS en una zona (el fondo de la planta de la casa) para que se lean
//  como un set separado del PSX Furniture Pack (que quedó centrado). Grupo único
//  "MueblesViejaDerelict" bajo FOLKLORE_MAP, nombres únicos → guardable con Save Map
//  Layout. Se llama desde HouseBuilder.BuildAlpHouse (antes de ApplySavedLayout).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class PsxDerelictFurnitureBuilder
    {
        const string Dir = "Assets/ExternalAssets/PSXDerelict/";

        // (archivo, fracción X, fracción Z del footprint, yaw). El set PSX Derelict va
        // agrupado en el FONDO (fz alto) → aparte del otro pack (centrado). "double_bed"
        // es un asset SUELTO (Low Poly PSX Double Bed, Sketchfab — ver ASSET_CREDITS): va
        // en un rincón tipo dormitorio (frente-izquierda). Todo a tamaño real (escala 1).
        static readonly (string glb, float fx, float fz, float yaw)[] Items =
        {
            ("couch",      0.25f, 0.83f,   0f),
            ("chair",      0.40f, 0.86f, 200f),
            ("mattress",   0.55f, 0.86f,  15f),
            ("vase",       0.66f, 0.81f,   0f),
            ("fridge",     0.78f, 0.85f, 180f),
            ("shelf",      0.86f, 0.73f, -90f),
            ("double_bed", 0.22f, 0.28f,  90f),   // cama doble (rincón dormitorio)
        };

        public static void Build(Transform mapRoot, Bounds hb)
        {
            var group = BuilderUtils.Group(mapRoot, "MueblesViejaDerelict", Vector3.zero);
            float floorY = hb.min.y + 0.05f;
            int placed = 0;
            foreach (var it in Items)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + it.glb + ".glb");
                if (prefab == null)
                {
                    Debug.LogWarning("[MueblesDerelict] falta " + Dir + it.glb + ".glb (¿Unity lo importó?).");
                    continue;
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                inst.name = it.glb;                       // nombre único por pieza
                inst.transform.localScale = Vector3.one;  // ya vienen a tamaño real
                inst.transform.rotation = Quaternion.Euler(0f, it.yaw, 0f);
                float x = Mathf.Lerp(hb.min.x, hb.max.x, it.fx);
                float z = Mathf.Lerp(hb.min.z, hb.max.z, it.fz);
                inst.transform.position = new Vector3(x, floorY, z);
                var b = PropBounds(inst);
                inst.transform.position += new Vector3(0f, floorY - b.min.y, 0f); // base en el piso

                MakeMatte(inst);   // sin brillo/especular (look PS1, parejo con el resto)

                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                placed++;
            }
            Debug.Log($"<color=lime>[MueblesDerelict] {placed} muebles abandonados (Daniel Jurys) en la casa de la vieja, " +
                      "aparte del otro set. Acomodalos a mano y Save Map Layout.</color>");
        }

        // La cama importó BRILLOSA vs el resto (el ajuste anterior no le pegó → su material no
        // era URP/Lit o tenía EMISIÓN, que la hace brillar ignorando la luz de escena). Fix robusto:
        // REARMA cada material como URP/Lit MATE (toma la textura base del importado), apaga
        // especular/reflejos y EMISIÓN. Sobre materiales NUEVOS (no toca el asset importado).
        static void MakeMatte(GameObject inst)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials;
                var outM = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var s = src[i];
                    if (s == null) { outM[i] = null; continue; }
                    var m = new Material(lit);

                    // textura base (varias convenciones de shader: URP, built-in, glTF)
                    Texture tex = (s.HasProperty("_BaseMap") ? s.GetTexture("_BaseMap") : null)
                               ?? (s.HasProperty("_MainTex") ? s.GetTexture("_MainTex") : null)
                               ?? (s.HasProperty("baseColorTexture") ? s.GetTexture("baseColorTexture") : null)
                               ?? s.mainTexture;
                    if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
                    Color col = s.HasProperty("_BaseColor") ? s.GetColor("_BaseColor")
                              : (s.HasProperty("_Color") ? s.GetColor("_Color") : Color.white);
                    m.SetColor("_BaseColor", col); m.color = col;

                    // MATE, sin reflejos
                    m.SetFloat("_Smoothness", 0f);
                    m.SetFloat("_Metallic", 0f);
                    m.SetFloat("_SpecularHighlights", 0f);
                    m.SetFloat("_EnvironmentReflections", 0f);
                    m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    // sin EMISIÓN (que la cama no brille sola)
                    m.DisableKeyword("_EMISSION");
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

                    outM[i] = m;
                }
                r.sharedMaterials = outM;
            }
        }

        static Bounds PropBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
