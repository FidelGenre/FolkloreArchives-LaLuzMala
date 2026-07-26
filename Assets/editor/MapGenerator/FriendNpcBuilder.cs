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
//   - Friend_FemaleSec:    modelo "a_lowpoly" (retro-style girl), bajado por el owner
//     como girl-game-character-retro-style.zip — SIN readme/licencia en el zip;
//     anotar la fuente/crédito cuando el owner la tenga a mano.
//   - Friend_MaleGreenJkt: "Character_Male_GreenJacket (Rigged)" by Wardster (Sketchfab, CC Attribution)
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class FriendNpcBuilder
    {
        const string Dir = "Assets/ExternalAssets/FriendNPCs/";

        // par (nombre de submaterial del FBX, textura a usarle) -- para modelos que
        // vienen con VARIOS materiales (p.ej. body/hair/shoes) en vez de una textura
        // única para todo el modelo.
        struct TexPart
        {
            public string match, tex;
            public TexPart(string m, string t) { match = m; tex = t; }
        }

        struct FriendDef
        {
            public string name, fbx, tex;
            public TexPart[] texParts; // no-null = multi-material (match por nombre de submaterial); null = usar `tex` para todo el modelo
            public float targetHeight, offX, offZ, yaw;
            public FolkloreArchives.HumanWalkAnim.Limb[] limbs; // null = usar los default de HumanWalkAnim (rig estilo Blender: thigh.L/upper_arm.L)
            public FriendDef(string n, string f, string tx, float h, float ox, float oz, float y, FolkloreArchives.HumanWalkAnim.Limb[] customLimbs = null, TexPart[] parts = null)
            { name = n; fbx = f; tex = tx; targetHeight = h; offX = ox; offZ = oz; yaw = y; limbs = customLimbs; texParts = parts; }
        }

        // owner: "que no esten todos duros en pose de t" -- HumanWalkAnim corrige la pose
        // de brazos (T-pose -> brazos abajo) calculando su dirección real hombro->mano,
        // pero para eso necesita encontrar los huesos por NOMBRE EXACTO -- cada uno de
        // estos 3 personajes viene de un pack distinto con su PROPIO rig/nomenclatura
        // (confirmado leyendo los nombres de hueso crudos de cada .fbx), así que los 3
        // necesitan su propia lista de Limb[] (ninguno usa los defaults de HumanWalkAnim).
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
        static readonly FolkloreArchives.HumanWalkAnim.Limb[] GirlRetroLimbs =
        {
            // "a_lowpoly" (girl-game-character-retro-style.zip): rig tipo UE Mannequin
            // (confirmado leyendo los nombres de hueso crudos del fbx: root/spine_01/
            // spine_02/arm_l/forearm_l/hand_l/thigh_l...), sufijo _l/_r sin "upper_".
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "thigh_l", phase =  1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "thigh_r", phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "arm_l",   phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "arm_r",   phase =  1f },
        };
        // el modelo trae 3 materiales separados (body/hair/shoes) en vez de un atlas
        // único -- se matchea por nombre de submaterial (case-insensitive, "Contains").
        static readonly TexPart[] GirlRetroTex =
        {
            new TexPart("body",  Dir + "GirlRetro/Textures/body_tex.png"),
            new TexPart("hair",  Dir + "GirlRetro/Textures/hair_tex.png"),
            new TexPart("shoes", Dir + "GirlRetro/Textures/shoes_tex.png"),
        };

        static readonly FriendDef[] Friends =
        {
            // al costado sur de la ruta, cerca del auto (que queda en offset 0,0), mirando hacia el auto (+X)
            new FriendDef("Friend_MaleCasual",   Dir + "MaleCasual/male_casual.fbx",           Dir + "MaleCasual/man_tex.png",           2.3f, -4.5f,  3.0f, 100f, MaleCasualLimbs),
            // al costado norte de la ruta, mirando hacia el auto (+X)
            new FriendDef("Friend_MaleGreenJkt", Dir + "MaleGreenJacket/BlackMan_W_Mullet.fbx", Dir + "MaleGreenJacket/BMMtxt.png",        2.3f, -5.0f, -3.0f,  80f, MixamoLimbs),
            // un poco más atrás, entre los otros dos, mirando hacia el auto (+X) --
            // owner: "descargue esa chica descomprimila y reemplazala por la que ya
            // esta" -- reemplaza a la vieja "PSX Female Secretary" de Vinrax (no
            // pegaba con la ambientación rural). Misma posición/altura que antes.
            new FriendDef("Friend_FemaleSec",    Dir + "GirlRetro/girl_retro.fbx",              null,                                      2.3f, -8.0f,  0.2f,  90f, GirlRetroLimbs, GirlRetroTex),
        };

        // roadCenter: mismo punto (X,Z) donde arranca el auto manejable (CarBuilder.cs,
        // MapLayout.MapSizeX - 30f) -- así los amigos quedan al lado, no adentro del auto.
        public static void Build(Transform root, Terrain t, Vector2 roadCenter)
        {
            var group = BuilderUtils.Group(root, "FriendsNPC", BuilderUtils.Ground(t, roadCenter.x, roadCenter.y));
            foreach (var f in Friends)
                BuildOne(group, t, f, roadCenter);
        }

        // owner: "hace que se puedan sentar no mas en el auto decorativos" -- llamado
        // por CarBuilder DESPUÉS de armar el auto (Build de acá arriba corre antes,
        // desde LandmarkBuilder, cuando el auto todavía no existe). Sienta a los 3
        // amigos ya construidos en los 3 asientos libres (conductor = jugador):
        // acompañante, trasero izq., trasero der. — mismo orden que `Friends`.
        // Puramente decorativo: sin interacción/E, no caminan más (se les saca
        // FriendWander) y quedan fijos con la MISMA pose "sentado" que ya usa el
        // jugador/perro (HumanWalkAnim.seated).
        // owner: "estan atravezando el asiento de abajo" -- el primer intento restaba
        // la altura de OJO completa (2.3, mismo offset que usa PlayerVehicleInteractor
        // para el jugador PARADO), pero acá el personaje queda VISIBLE (al jugador no
        // se le ve el propio cuerpo sentado, por el layer SelfHidden -- nunca se notó
        // este problema con el jugador). HumanWalkAnim.seated solo ROTA los muslos, no
        // traslada el modelo -- restar la altura de ojo entera hunde la cadera/raíz
        // muy por debajo del asiento real (el cuerpo entero quedaba atravesando el
        // piso del auto). Restando solo la altura de CADERA aprox. (mitad del alto del
        // personaje) la cadera queda a la altura del asiento y listo.
        const float SeatHipHeight = 1.15f; // ~mitad de targetHeight (2.3) -- altura cadera parada

        public static void SeatInCar(Transform root, FolkloreArchives.CarController car)
        {
            if (car == null) return;
            var group = root.Find("PointsOfInterest/FriendsNPC");
            if (group == null) { Debug.LogWarning("FriendNpc: no encontré FriendsNPC para sentar en el auto."); return; }

            Transform[] seats = { car.frontPassenger, car.rearLeft, car.rearRight };
            for (int i = 0; i < Friends.Length && i < seats.Length; i++)
            {
                var seat = seats[i];
                var friend = group.Find(Friends[i].name);
                if (seat == null || friend == null) continue;

                var wander = friend.GetComponent<FolkloreArchives.FriendWander>();
                if (wander != null) Object.DestroyImmediate(wander);

                // seat.localRotation siempre es identity (ver Seat() en CarBuilder), así
                // que se puede trabajar directo en el espacio LOCAL del auto.
                friend.SetParent(car.transform, false);
                friend.localRotation = Quaternion.identity;
                friend.localPosition = seat.localPosition - new Vector3(0f, SeatHipHeight, 0f);

                var anim = friend.GetComponent<FolkloreArchives.HumanWalkAnim>();
                if (anim != null) anim.seated = true;
            }
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

            // material(es) URP propio(s) (si no, el FBX trae Standard = magenta en URP)
            if (f.texParts != null)
            {
                // modelo multi-material (p.ej. body/hair/shoes): un material URP por
                // TEXTURA (cacheado acá para no duplicar si varias submallas comparten
                // la misma parte), asignado por nombre de submaterial original del fbx.
                var matCache = new System.Collections.Generic.Dictionary<string, Material>();
                foreach (var r in rends)
                {
                    var src = r.sharedMaterials;
                    var arr = new Material[src.Length];
                    for (int k = 0; k < src.Length; k++)
                    {
                        string srcName = src[k] != null ? src[k].name.ToLowerInvariant() : "";
                        string texPath = f.texParts[0].tex; // fallback: primera parte
                        foreach (var p in f.texParts)
                            if (srcName.Contains(p.match)) { texPath = p.tex; break; }

                        if (!matCache.TryGetValue(texPath, out var m))
                        {
                            var tex = LoadPointTex(texPath);
                            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
                            string matPath = "Assets/Settings/PSX_" + f.name + "_" + System.IO.Path.GetFileNameWithoutExtension(texPath) + ".mat";
                            AssetDatabase.DeleteAsset(matPath);
                            AssetDatabase.CreateAsset(m, matPath);
                            matCache[texPath] = m;
                        }
                        arr[k] = m;
                    }
                    r.sharedMaterials = arr;
                }
            }
            else
            {
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
