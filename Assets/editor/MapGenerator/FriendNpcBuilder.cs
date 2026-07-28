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
            // owner: ajustó a mano, en vivo (Play), la posición LOCAL sentada de este
            // amigo dentro del auto (arrastrando el gizmo / tocando Position en el
            // Inspector) hasta que "quedó perfecto" -- no-null = usar ESTE valor exacto
            // en vez de calcularlo con la fórmula seat-SeatRootOffset (que costó mucho
            // afinar a ciegas por captura).
            public Vector3? seatPosOverride;
            // owner: "tiene las piernas alrevez, daselas vuelta al malegreen" -- cada
            // personaje viene de un rig distinto (Vinrax, Mixamo, UE Mannequin), con el
            // eje del muslo orientado distinto -- el MISMO ángulo (seatedThighAngle,
            // compartido por HumanWalkAnim) dobla para adelante en uno y para atrás en
            // otro. No-null = usar ESTE ángulo en vez del default del componente, solo
            // para este personaje.
            public float? seatedThighAngleOverride;
            // owner: ajustó Seated Model Drop en vivo para este personaje puntual (le
            // hacía falta un valor distinto al default global de HumanWalkAnim).
            public float? seatedModelDropOverride;
            public float? seatedScaleYOverride;
            public FriendDef(string n, string f, string tx, float h, float ox, float oz, float y, FolkloreArchives.HumanWalkAnim.Limb[] customLimbs = null, TexPart[] parts = null)
            { name = n; fbx = f; tex = tx; targetHeight = h; offX = ox; offZ = oz; yaw = y; limbs = customLimbs; texParts = parts; seatPosOverride = null; seatedThighAngleOverride = null; seatedModelDropOverride = null; seatedScaleYOverride = null; }
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
            // owner (2da vuelta, "aparezco sentado encima del malegreen, ninguno lo
            // pusiste en el orden que te pedi"): BUG REAL -- al reasignar los 3 amigos
            // a asientos nuevos, los seatPosOverride VIEJOS quedaron puestos (son
            // posiciones ABSOLUTAS, no relativas al asiento que se les pasa) -- cada
            // uno seguía clavado en las coordenadas de SU asiento ANTERIOR sin importar
            // a qué seat apuntara el código. Este personaje ahora va al CONDUCTOR, un
            // asiento que nunca tuvo un amigo decorativo antes (sin precedente) -- sin
            // override, cae al fallback de la fórmula (seat - SeatRootOffset). Va a
            // necesitar el mismo ajuste en vivo que todos los demás asientos.
            new FriendDef("Friend_MaleCasual",   Dir + "MaleCasual/male_casual.fbx",           Dir + "MaleCasual/man_tex.png",           2.3f, -4.5f,  3.0f, 100f, MaleCasualLimbs),
            // al costado norte de la ruta, mirando hacia el auto (+X)
            // owner: "tiene las piernas alrevez" sentado -- rig Mixamo, eje del muslo
            // orientado al revés que el de Vinrax. Ángulo +55 sigue siendo válido (es
            // de SU rig, no del asiento).
            // owner (2da vuelta): ahora va a rearRight -- reusa la posición que SÍ era
            // de rearRight (la vieja de Friend_FemaleSec, que estaba ahí antes) en vez
            // de la propia (que era de rearLeft, donde ahora se sienta el jugador real
            // -- de ahí "encima del malegreen"). Sigue siendo una aproximación, no
            // ajustada en vivo para ESTE personaje en ESTE asiento todavía.
            new FriendDef("Friend_MaleGreenJkt", Dir + "MaleGreenJacket/BlackMan_W_Mullet.fbx", Dir + "MaleGreenJacket/BMMtxt.png",        2.3f, -5.0f, -3.0f,  80f, MixamoLimbs)
                { seatPosOverride = new Vector3(0.6090f, -0.1883f, -0.7f), seatedThighAngleOverride = 55f, seatedModelDropOverride = -0.5f },
            // un poco más atrás, entre los otros dos, mirando hacia el auto (+X) --
            // owner: "descargue esa chica descomprimila y reemplazala por la que ya
            // esta" -- reemplaza a la vieja "PSX Female Secretary" de Vinrax.
            // owner (2da vuelta): ahora va a frontPassenger -- reusa la posición que SÍ
            // era de frontPassenger (la vieja de Friend_MaleCasual, que estaba ahí
            // antes). El ángulo/drop/escala son de SU rig, se mantienen.
            // owner: "cuando va adelante hay que subirle un poco las piernas y moverla
            // apenitas para adelante asi no atravieza" -- piernas más dobladas
            // (thighAngle más negativo, mismo sentido "hacia adelante" que el default
            // -62 de HumanWalkAnim) + un empujón chico hacia +Z (adelante del auto, ver
            // CarBuilder: paxBase resta Z para atrás, así que +Z es hacia el
            // tablero/parabrisas). Primer ajuste a ojo -- falta confirmar en vivo como
            // el resto de las poses de esta sesión.
            new FriendDef("Friend_FemaleSec",    Dir + "GirlRetro/girl_retro.fbx",              null,                                      2.3f, -8.0f,  0.2f,  90f, GirlRetroLimbs, GirlRetroTex)
                { seatPosOverride = new Vector3(0.5999f, -0.283f, 0.38f), seatedThighAngleOverride = -72f, seatedModelDropOverride = -0.5f, seatedScaleYOverride = 0.76f },
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
        // desde LandmarkBuilder, cuando el auto todavía no existe).
        // owner (2da vuelta, "vamos todos en el auto desde el inicio de mapa hasta la
        // gasolinera"): los 3 amigos van AHORA en conductor/rearRight/acompañante --
        // MaleCasual maneja (la secuencia de apertura, OpeningDriveSequence.cs, mueve
        // el auto solo; MaleCasual solo necesita la pose), FemaleSec de acompañante,
        // MaleGreenJkt en el asiento trasero libre. `rearLeft`/`rearMid` quedan SIN
        // TOCAR acá a propósito -- los reserva OpeningDriveSequence para sentar ahí al
        // jugador y al perro REALES al arrancar el juego. Mismo orden que `Friends`.
        // Puramente decorativo: sin interacción/E, no caminan más (se les saca
        // FriendWander) y quedan fijos con la MISMA pose "sentado" que ya usa el
        // jugador/perro (HumanWalkAnim.seated).
        // owner: varias vueltas moviendo esto SOLO (1.15 → 0.65 → 1.0) nunca convergió
        // -- "hundido" y "flotando sobre el techo" pasaban casi con el mismo valor. La
        // razón real: mover la raíz no ACHICA al personaje, solo lo desplaza entero --
        // un personaje de 2.3m de pie no entra bajo el techo de un auto sin importar
        // DÓNDE se ubique la raíz. El fix real ahora vive en HumanWalkAnim.seated
        // (achica+baja el modelo, ver seatedScaleY/seatedModelDrop). Acá alcanza con
        // alinear la CABEZA a la altura del asiento (raíz = seat - altura completa,
        // como si estuviera parado) — el achicado de HumanWalkAnim se encarga de bajar
        // esa cabeza a una altura de sentado razonable y de no hundir los pies.
        const float SeatRootOffset = 2.3f; // = targetHeight de los 3 amigos

        public static void SeatInCar(Transform root, FolkloreArchives.CarController car)
        {
            if (car == null) return;
            var group = root.Find("PointsOfInterest/FriendsNPC");
            if (group == null) { Debug.LogWarning("FriendNpc: no encontré FriendsNPC para sentar en el auto."); return; }

            Transform[] seats = { car.driverSeat, car.rearRight, car.frontPassenger };
            for (int i = 0; i < Friends.Length && i < seats.Length; i++)
            {
                var f = Friends[i];
                var seat = seats[i];
                var friend = group.Find(f.name);
                if (seat == null || friend == null) continue;

                var wander = friend.GetComponent<FolkloreArchives.FriendWander>();
                if (wander != null) Object.DestroyImmediate(wander);

                // owner: "no spawnearon ahora" -- diagnóstico defensivo: si el modelo
                // visual no está (por lo que sea), avisar FUERTE en vez de sentar un
                // GameObject invisible en silencio.
                if (friend.Find("Model") == null)
                    Debug.LogWarning($"FriendNpc: {friend.name} no tiene hijo 'Model' -- se sienta igual pero no se va a ver. Revisar BuildOne/el fbx.");

                // seat.localRotation siempre es identity (ver Seat() en CarBuilder), así
                // que se puede trabajar directo en el espacio LOCAL del auto.
                friend.SetParent(car.transform, false);
                friend.localRotation = Quaternion.identity;
                friend.localPosition = f.seatPosOverride ?? (seat.localPosition - new Vector3(0f, SeatRootOffset, 0f));

                var anim = friend.GetComponent<FolkloreArchives.HumanWalkAnim>();
                if (anim != null) anim.seated = true;
            }
        }

        static void BuildOne(Transform parent, Terrain t, FriendDef f, Vector2 c)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(f.fbx);
            if (fbx == null) { Debug.LogWarning("FriendNpc: no encontré " + f.fbx); return; }

            // owner: "no spawnearon ahora" -- dureza defensiva: antes se reimportaba la
            // textura (LoadPointTex → SaveAndReimport) DESPUÉS de instanciar el modelo
            // del fbx, en la MISMA pasada. Reimportar un asset a mitad de construir la
            // jerarquía es terreno conocido para comportamiento errático del Editor. Se
            // adelanta ACÁ, antes de tocar nada del GameObject, para que cualquier
            // reimport/refresh de AssetDatabase quede resuelto antes de instanciar.
            if (f.texParts != null) foreach (var p in f.texParts) LoadPointTex(p.tex);
            else LoadPointTex(f.tex);

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
            if (f.seatedThighAngleOverride.HasValue) anim.seatedThighAngle = f.seatedThighAngleOverride.Value;
            if (f.seatedModelDropOverride.HasValue) anim.seatedModelDrop = f.seatedModelDropOverride.Value;
            if (f.seatedScaleYOverride.HasValue) anim.seatedScaleY = f.seatedScaleYOverride.Value;

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
