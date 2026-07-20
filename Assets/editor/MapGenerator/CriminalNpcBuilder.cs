// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CriminalNpcBuilder.cs — los 5 ladrones/asesinos enmascarados
//  (owner: "4-5 mejor" + capturas del pack "Characters PSX" de
//  Elbolilloduro eligiendo justo estos 5 disfraces). Mismo
//  tratamiento PSX que FriendNpcBuilder: estáticos por ahora
//  (sin IA/animación todavía), material URP propio + textura en
//  filtro Point.
//
//  Los 5 van juntos en MainCriminalCamp (owner: "quiero que esten
//  los 5 ahi en ese campamento malo"). El campamento tiene 4 ranchos
//  a ~14-16m del centro (CriminalCampBuilder los escala ×1.6), así
//  que el claro alrededor de la fogata queda libre hasta bastante
//  más lejos que eso — los 5 se paran en ese claro (radio 3.6-6.5m),
//  con margen de sobra para no pisar ningún rancho.
//
//  Créditos: "Characters PSX" pack by Elbolilloduro (itch.io, CC0)
//   - Character_Killer      → máscara de arpillera + leñador + overol
//   - Character_Killer_01   → máscara de arpillera + buzo con capucha
//   - Character_Killer_02   → máscara de calavera + campera de jean
//   - Character_Killer_05   → máscara de chancho + remera + overol  (líder)
//   - Character_Killer_06   → cara ensangrentada + uniforme blanco
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class CriminalNpcBuilder
    {
        const string Dir = "Assets/ExternalAssets/CriminalNPCs/";

        struct CriminalDef
        {
            public string name, fbx, tex;
            public float targetHeight, offX, offZ, yaw;
            public CriminalDef(string n, string f, string tx, float h, float ox, float oz, float y)
            { name = n; fbx = f; tex = tx; targetHeight = h; offX = ox; offZ = oz; yaw = y; }
        }

        static readonly CriminalDef[] Criminals =
        {
            // líder (máscara de chancho), más cerca de la fogata, mirando hacia el centro
            new CriminalDef("Criminal_PigMask_Leader",      Dir + "Killer_PigMask/Character_Killer_05.fbx",          Dir + "Killer_PigMask/Character_Killer_05.png",           2.25f, -2f,  -3f,  34f),
            // 4 guardias, en las 4 direcciones cardinales alrededor de la fogata
            new CriminalDef("Criminal_SackheadFlannel",     Dir + "Killer_Sackhead_Flannel/Character_Killer.fbx",    Dir + "Killer_Sackhead_Flannel/Character_Killer.png",    2.2f,  0f,   6.5f, 180f),
            new CriminalDef("Criminal_SkullJacket",         Dir + "Killer_SkullJacket/Character_Killer_02.fbx",      Dir + "Killer_SkullJacket/Character_Killer_02.png",      2.2f,  6.5f, 0f,   -90f),
            new CriminalDef("Criminal_SackheadHoodie",      Dir + "Killer_Sackhead_Hoodie/Character_Killer_01.fbx",  Dir + "Killer_Sackhead_Hoodie/Character_Killer_01.png",  2.2f,  0f,  -6.5f,  0f),
            new CriminalDef("Criminal_BloodyUniform",       Dir + "Killer_BloodyUniform/Character_Killer_06.fbx",    Dir + "Killer_BloodyUniform/Character_Killer_06.png",    2.2f, -6.5f, 0f,   90f),
        };

        public static void Build(Transform criminalCamp, Terrain t, Vector2 campCenter)
        {
            // el pack se descomprimió recién este mismo Generate en algunos casos —
            // fuerza el import antes de pedir LoadAssetAtPath, si no puede devolver null.
            AssetDatabase.Refresh();

            var group = BuilderUtils.Group(criminalCamp, "CriminalsNPC", BuilderUtils.Ground(t, campCenter.x, campCenter.y));
            foreach (var c in Criminals) BuildOne(group, t, c, campCenter);
        }

        static void BuildOne(Transform parent, Terrain t, CriminalDef f, Vector2 c)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(f.fbx);
            if (fbx == null) { Debug.LogWarning("CriminalNpc: no encontré " + f.fbx); return; }

            float wx = c.x + f.offX, wz = c.y + f.offZ;
            Vector3 pos = BuilderUtils.Ground(t, wx, wz);
            var go = new GameObject(f.name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, f.yaw, 0f);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Model";
            model.transform.SetParent(go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Debug.LogWarning("CriminalNpc: " + f.name + " (" + f.fbx + ") no tiene ningún Renderer."); return; }

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = Mathf.Max(0.0001f, b.size.y);
            model.transform.localScale = Vector3.one * (f.targetHeight / h);

            // replanta los pies en y=0 (los bounds cambiaron con la escala nueva)
            Bounds b2 = rends[0].bounds;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) b2.Encapsulate(r.bounds);
            model.transform.localPosition = new Vector3(0f, -(b2.min.y - go.transform.position.y), 0f);

            // material URP propio (si no, el FBX trae Standard = magenta en URP)
            var tex = LoadPointTex(f.tex);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            string matPath = "Assets/Settings/PSX_" + f.name + ".mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            foreach (var r in rends)
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                r.sharedMaterials = arr;
            }
        }

        static Texture2D LoadPointTex(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.filterMode != FilterMode.Point)
            {
                imp.filterMode = FilterMode.Point;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
