// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  FriendNpcBuilder.cs — los 3 amigos del protagonista. Por ahora
//  son estáticos (sin IA/diálogo/animación todavía), mismo
//  tratamiento PSX (material URP + textura con filtro Point) que
//  NetworkBuilder usa para el modelo del jugador.
//  owner: "deben spawnear tambien en la ruta del mismo lado porque
//  arrancan la historia todos juntos" -- antes estaban parados en el
//  campamento (la fogata); ahora arrancan junto al auto manejable
//  (Renault12, lado ESTE de la ruta -- ver CarBuilder.cs), parados
//  al costado de la ruta esperando/charlando cerca del auto.
//  "y deben medir lo mismo que el personaje principal" -- las 3
//  alturas eran inconsistentes (2.2/2.2/2.1); ahora las 3 miden
//  2.3f, igual que NetworkBuilder.BuildPersonVisual (target del
//  protagonista/red).
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
            public FolkloreArchives.HumanWalkAnim.Limb[] limbs; // null = usar los default de HumanWalkAnim (rig estilo Blender: thigh.L/upper_arm.L)
            public FriendDef(string n, string f, string tx, float h, float ox, float oz, float y, FolkloreArchives.HumanWalkAnim.Limb[] customLimbs = null)
            { name = n; fbx = f; tex = tx; targetHeight = h; offX = ox; offZ = oz; yaw = y; limbs = customLimbs; }
        }

        // owner: "que no esten todos duros en pose de t" -- HumanWalkAnim corrige la pose
        // de brazos (T-pose -> brazos abajo) calculando su dirección real hombro->mano,
        // pero para eso necesita encontrar los huesos por NOMBRE EXACTO -- cada uno de
        // estos 3 personajes viene de un pack distinto con su PROPIO rig/nomenclatura
        // (confirmado leyendo los nombres de hueso crudos de cada .fbx). Los defaults de
        // HumanWalkAnim (thigh.L / upper_arm.L, notación Blender) solo coinciden con
        // FemaleSecretary -- los otros dos necesitan su propia lista.
        static readonly FolkloreArchives.HumanWalkAnim.Limb[] MaleCasualLimbs =
        {
            // Vinrax "PSX Casual Male": snake_case con sufijo _left/_right
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "thigh_left",      phase =  1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "thigh_right",     phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "upper_arm_left",  phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "upper_arm_right", phase =  1f },
        };
        static readonly FolkloreArchives.HumanWalkAnim.Limb[] MixamoLimbs =
        {
            // Wardster "Character_Male_GreenJacket": rig Mixamo (prefijo "mixamorig:")
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:LeftUpLeg",  phase =  1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:RightUpLeg", phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:LeftArm",    phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:RightArm",   phase =  1f },
        };

        static readonly FriendDef[] Friends =
        {
            // al costado sur de la ruta, cerca del auto (que queda en offset 0,0), mirando hacia el auto (+X)
            new FriendDef("Friend_MaleCasual",   Dir + "MaleCasual/male_casual.fbx",           Dir + "MaleCasual/man_tex.png",           2.3f, -4.5f,  3.0f, 100f, MaleCasualLimbs),
            // al costado norte de la ruta, mirando hacia el auto (+X)
            new FriendDef("Friend_MaleGreenJkt", Dir + "MaleGreenJacket/BlackMan_W_Mullet.fbx", Dir + "MaleGreenJacket/BMMtxt.png",        2.3f, -5.0f, -3.0f,  80f, MixamoLimbs),
            // un poco más atrás, entre los otros dos, mirando hacia el auto (+X) -- rig ya
            // usa notación thigh.L/upper_arm.L, coincide con los defaults de HumanWalkAnim
            new FriendDef("Friend_FemaleSec",    Dir + "FemaleSecretary/female_secretary.fbx",  Dir + "FemaleSecretary/secretary_tex.png", 2.3f, -8.0f,  0.2f,  90f),
        };

        // roadCenter: mismo punto (X,Z) donde arranca el auto manejable (CarBuilder.cs,
        // MapLayout.MapSizeX - 30f) -- así los amigos quedan al lado, no adentro del auto.
        public static void Build(Transform root, Terrain t, Vector2 roadCenter)
        {
            var group = BuilderUtils.Group(root, "FriendsNPC", BuilderUtils.Ground(t, roadCenter.x, roadCenter.y));
            foreach (var f in Friends)
                BuildOne(group, t, f, roadCenter);
        }

        static void BuildOne(Transform parent, Terrain t, FriendDef f, Vector2 c)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(f.fbx);
            if (fbx == null) { Debug.LogWarning("FriendNpc: no encontré " + f.fbx); return; }

            float wx = c.x + f.offX, wz = c.y + f.offZ;
            Vector3 pos = BuilderUtils.Ground(t, wx, wz);
            // owner: "no estan" -- mismo bug que ya se había arreglado en CarBuilder/
            // TestPlayerBuilder ("esta spwaneado debajo de la tierra"): cerca del borde
            // este del mapa el terreno CRUDO queda más bajo que la ruta pavimentada
            // real (que es otra malla encima), así que samplear el terreno solo
            // enterraba a los amigos bajo la ruta. Ídem acá: piso el mayor entre los dos.
            pos.y = Mathf.Max(MapLayout.RoadSurfaceHeight, pos.y);
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

            // owner: "que no esten todos duros en pose de t" -- misma animación
            // procedural que usa el protagonista. Parados (sin moverse), el ciclo de
            // caminata no hace nada (amp llega a 0 solo) pero SÍ corrige la pose de
            // reposo: brazos bajados en vez de en cruz (T-pose).
            var anim = go.AddComponent<FolkloreArchives.HumanWalkAnim>();
            if (f.limbs != null) anim.limbs = f.limbs;

            // owner: "necesito ver como caminan" -- deambulan de a poco cerca de donde
            // arrancan (no es IA real, solo para que no queden parados como estatuas).
            // owner: "este tiene los pies bajo tierra" -- FriendWander es un script
            // runtime, no puede ver MapLayout (editor-only); le pasamos el piso mínimo
            // (mismo Mathf.Max que ya usa el spawn de acá arriba) para que no se hunda
            // apenas empieza a caminar.
            var wander = go.AddComponent<FolkloreArchives.FriendWander>();
            wander.minGroundY = MapLayout.RoadSurfaceHeight;
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
