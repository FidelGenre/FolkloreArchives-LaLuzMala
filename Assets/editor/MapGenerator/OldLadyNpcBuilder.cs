// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  OldLadyNpcBuilder.cs — la vieja cuentacuentos, parada afuera de
//  su casa (OldLadyHouseCenter), del lado por donde entra el camino
//  (AlpHouseYaw=180 ya gira la casa para que la entrada mire hacia
//  ahí — ver HouseBuilder.cs). Mismo tratamiento PSX que
//  FriendNpcBuilder/CriminalNpcBuilder: estática por ahora (sin
//  IA/diálogo/animación todavía), material URP propio + textura en
//  filtro Point.
//
//  Crédito: "Characters PSX" pack by Elbolilloduro (itch.io, CC0) —
//  Character_31_Female (cardigan celeste + pelo canoso).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class OldLadyNpcBuilder
    {
        const string Fbx = "Assets/ExternalAssets/OldLadyNPC/Character_31_Female.fbx";
        const string Tex = "Assets/ExternalAssets/OldLadyNPC/Character_31_Female.png";
        const float TargetHeight = 2.0f; // vieja: un poco más baja que los adultos (2.2)

        public static void Build(Transform parent, Terrain t)
        {
            AssetDatabase.Refresh();

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (fbx == null) { Debug.LogWarning("OldLadyNpc: no encontré " + Fbx); return; }

            Vector2 c = MapLayout.OldLadyHouseCenter;
            // unos metros hacia el NE del centro de la casa: por ahí entra el camino
            // (Camino10 llega desde el campamento, que queda al NE), y AlpHouseYaw=180
            // ya orienta la entrada de la casa hacia ese lado.
            float wx = c.x + 3f, wz = c.y + 2f;
            Vector3 pos = BuilderUtils.Ground(t, wx, wz);

            var go = new GameObject("OldLady_Storyteller");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 225f, 0f); // mirando hacia la casa/SO

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Model";
            model.transform.SetParent(go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Debug.LogWarning("OldLadyNpc: el modelo no tiene ningún Renderer."); return; }

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = Mathf.Max(0.0001f, b.size.y);
            model.transform.localScale = Vector3.one * (TargetHeight / h);

            // replanta los pies en y=0 (los bounds cambiaron con la escala nueva)
            Bounds b2 = rends[0].bounds;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) b2.Encapsulate(r.bounds);
            model.transform.localPosition = new Vector3(0f, -(b2.min.y - go.transform.position.y), 0f);

            // material URP propio (si no, el FBX trae Standard = magenta en URP)
            var tex = LoadPointTex(Tex);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            string matPath = "Assets/Settings/PSX_OldLady_Storyteller.mat";
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
