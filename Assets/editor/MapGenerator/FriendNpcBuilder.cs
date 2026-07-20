// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  FriendNpcBuilder.cs — los 3 amigos del protagonista. Por ahora
//  son estáticos (sin IA/diálogo/animación todavía), parados
//  alrededor de la fogata del campamento, mismo tratamiento PSX
//  (material URP + textura con filtro Point) que NetworkBuilder
//  usa para el modelo del jugador. Se ubican en el lado oeste/este/
//  norte de la fogata para no pisar SPAWN_PLAYER1/SPAWN_RUFUS
//  (ambos del lado sur, por donde entra el jugador al campamento).
//
//  Créditos (assets gratuitos bajados por el owner):
//   - Friend_MaleCasual:   "PSX Casual Male Character" by Vinrax (itch.io, free — credit required)
//   - Friend_FemaleSec:    "PSX Female Secretary Character" by Vinrax (itch.io, free — credit required)
//   - Friend_MaleGreenJkt: "Character_Male_GreenJacket (Rigged)" by Wardster (Sketchfab, CC Attribution)
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class FriendNpcBuilder
    {
        const string Dir = "Assets/ExternalAssets/FriendNPCs/";

        struct FriendDef
        {
            public string name, fbx, tex;
            public float targetHeight, offX, offZ, yaw;
            public FriendDef(string n, string f, string tx, float h, float ox, float oz, float y)
            { name = n; fbx = f; tex = tx; targetHeight = h; offX = ox; offZ = oz; yaw = y; }
        }

        static readonly FriendDef[] Friends =
        {
            // oeste de la fogata, mirando hacia el fuego (+X)
            new FriendDef("Friend_MaleCasual",   Dir + "MaleCasual/male_casual.fbx",           Dir + "MaleCasual/man_tex.png",           2.2f, -3.9f,  0.1f,  90f),
            // este de la fogata, mirando hacia el fuego (-X)
            new FriendDef("Friend_MaleGreenJkt", Dir + "MaleGreenJacket/BlackMan_W_Mullet.fbx", Dir + "MaleGreenJacket/BMMtxt.png",        2.2f,  3.9f, -0.2f, -90f),
            // norte de la fogata (entre el fuego y las carpas), mirando hacia el sur
            new FriendDef("Friend_FemaleSec",    Dir + "FemaleSecretary/female_secretary.fbx",  Dir + "FemaleSecretary/secretary_tex.png", 2.1f,  0.2f,  2.6f, 180f),
        };

        public static void Build(Transform root, Terrain t, Vector2 campCenter)
        {
            var group = BuilderUtils.Group(root, "FriendsNPC", BuilderUtils.Ground(t, campCenter.x, campCenter.y));
            foreach (var f in Friends)
                BuildOne(group, t, f, campCenter);
        }

        static void BuildOne(Transform parent, Terrain t, FriendDef f, Vector2 c)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(f.fbx);
            if (fbx == null) { Debug.LogWarning("FriendNpc: no encontré " + f.fbx); return; }

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
            if (rends.Length == 0) return;

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
