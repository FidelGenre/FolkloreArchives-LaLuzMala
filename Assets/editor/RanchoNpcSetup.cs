// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  RanchoNpcSetup.cs — pone al VIEJO (esposo de la vieja) parado en
//  la puerta de la LETRINA del rancho, DESACTIVADO. La secuencia de
//  la misión lo activa cuando el jugador toca la puerta (susto).
//
//  Construye el modelo del MISMO pack que la vieja ("Characters PSX"
//  de Elbolilloduro, CC0): Character_32 (canoso, sweater gris + camisa
//  + jeans). Mismo tratamiento que OldLadyNpcBuilder: material URP/Lit
//  propio con la textura en filtro Point, escala a altura objetivo,
//  pies en el piso.
//
//  USO: poné la letrina donde va y corré
//       Folklore ▸ Poner viejo del rancho en la letrina.
// ============================================================
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class RanchoNpcSetup
    {
        const string Fbx = "Assets/ExternalAssets/OldManNPC/Character_32.fbx";
        const string Tex = "Assets/ExternalAssets/OldManNPC/Character_32.png";
        const float  TargetHeight = 2.2f;  // owner: "tan alto como todos los NPCs" -- igual a los amigos adultos (la vieja es 2.0, más baja a propósito)
        const string SheepObj = "Assets/ExternalAssets/Sheep/sheep.obj";
        const string SheepTex = "Assets/ExternalAssets/Sheep/sheep_tex.jpg";
        const float  SheepHeight = 1.6f;    // altura de la oveja (owner: "más grandes")

        // el pack "Characters PSX" usa rig Mixamo (mixamorig:*): mismos limbs que el amigo
        // "green jacket". HumanWalkAnim con esto = camina + brazos a los lados (no T-pose).
        static readonly FolkloreArchives.HumanWalkAnim.Limb[] MixamoLimbs =
        {
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:LeftUpLeg",  phase =  1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:RightUpLeg", phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:LeftArm",    phase = -1f },
            new FolkloreArchives.HumanWalkAnim.Limb { bone = "mixamorig:RightArm",   phase =  1f },
        };

        [MenuItem("Folklore/Poner viejo del rancho en la letrina")]
        static void PlaceOldMan()
        {
            AssetDatabase.Refresh();

            var door = FindByName("letrina.007");           // puerta
            var body = FindByName("letrina.006");           // cuerpo (para saber hacia dónde es "afuera")
            if (door == null)
            {
                EditorUtility.DisplayDialog("Rancho", "No encontré 'letrina.007' (la puerta) en la escena.", "OK");
                return;
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (fbx == null)
            {
                EditorUtility.DisplayDialog("Rancho", "No encontré " + Fbx + " (¿lo importó Unity ya?).", "OK");
                return;
            }

            // dirección "hacia afuera": del cuerpo de la letrina hacia la puerta
            Vector3 doorPos = door.position;
            Vector3 outward = body != null ? (doorPos - body.position) : door.forward;
            outward.y = 0f;
            if (outward.sqrMagnitude < 1e-4f) outward = Vector3.forward;
            outward.Normalize();

            // si ya había uno, lo saco (idempotente)
            var prev = FindByName("RanchoViejo");
            if (prev != null) Object.DestroyImmediate(prev.gameObject);

            // raíz + modelo (igual criterio que OldLadyNpcBuilder)
            var go = new GameObject("RanchoViejo");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Model";
            model.transform.SetParent(go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Object.DestroyImmediate(go); EditorUtility.DisplayDialog("Rancho", "El FBX del viejo no tiene Renderers.", "OK"); return; }

            // escala a la altura objetivo
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = Mathf.Max(0.0001f, b.size.y);
            model.transform.localScale = Vector3.one * (TargetHeight / h);

            // replanta los pies en la base de la raíz (los bounds cambiaron con la escala)
            Bounds b2 = rends[0].bounds;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) b2.Encapsulate(r.bounds);
            model.transform.localPosition = new Vector3(0f, -(b2.min.y - go.transform.position.y), 0f);

            // material URP propio (si no, el FBX trae Standard = magenta en URP)
            var tex = LoadPointTex(Tex);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            mat = BuilderUtils.SaveMaterialStable(mat, "Assets/Settings/PSX_RanchoViejo.mat");
            foreach (var r in rends)
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                r.sharedMaterials = arr;
            }

            // colisión (cápsula parada) + animación procedural (camina + brazos abajo), igual amigos
            var col = go.AddComponent<CapsuleCollider>();
            col.height = TargetHeight; col.radius = TargetHeight * 0.16f; col.center = new Vector3(0f, TargetHeight * 0.5f, 0f);
            var anim = go.AddComponent<FolkloreArchives.HumanWalkAnim>();
            anim.limbs = MixamoLimbs;

            // la VIEJA (ya en la escena) también necesita la misma movilidad/pose
            EnsureMobility(FindByName("OldLady_Storyteller"));

            // pos: justo afuera de la puerta, apoyado en el piso (raycast al terreno)
            Vector3 p = doorPos + outward * 0.6f;
            if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 30f)) p.y = hit.point.y;
            else p.y = doorPos.y;
            go.transform.position = p;
            go.transform.rotation = Quaternion.LookRotation(outward, Vector3.up); // mira afuera (al jugador)

            go.SetActive(false);   // aparece cuando tocás la puerta (lo activa la secuencia)

            Undo.RegisterCreatedObjectUndo(go, "Poner viejo del rancho");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Rancho] 'RanchoViejo' (Character_32) puesto DESACTIVADO en " + p +
                      ", mirando afuera de la letrina. Lo activa la secuencia al tocar la puerta.");
        }

        // agrega HumanWalkAnim (camina + brazos a los lados) si no lo tiene ya
        static void EnsureMobility(Transform npc)
        {
            if (npc == null) return;
            if (npc.GetComponent<FolkloreArchives.HumanWalkAnim>() == null)
            {
                var a = npc.gameObject.AddComponent<FolkloreArchives.HumanWalkAnim>();
                a.limbs = MixamoLimbs;
                Debug.Log("[Rancho] HumanWalkAnim agregado a " + npc.name);
            }
        }

        // Arma una puerta/tranquera ABRIBLE a partir del objeto seleccionado (normalmente un
        // trozo de Combined Mesh que no se puede rotar/animar tal cual, como la puerta del
        // corral o una puerta de casa). Lee su AABB/material, crea una réplica-plank (Cube) con
        // bisagra en un EXTREMO (eje Y, como una puerta real) + CorralGate, y DESACTIVA el
        // original. La réplica arranca en LA MISMA pose que el original (bounds calcados) --
        // CorralGate graba esa pose como "cerrada" al entrar a Play. Reusado por la tranquera del
        // corral y la puerta de la casa.
        static GameObject BuildHingeDoor(GameObject sel, string name, float openDeg, string hintClosed, string hintOpen)
        {
            var rend = sel.GetComponent<Renderer>();
            var mf = sel.GetComponent<MeshFilter>();
            if (rend == null || mf == null || mf.sharedMesh == null) return null;

            // owner: "hiciste cagada de nuevo, mira ahora desapareció la tranquera" -- Cube.184 es
            // Combined Mesh (static-batcheado): sus vértices YA están horneados en otro lado, así
            // que mf.sharedMesh.bounds (bounds LOCALES) no tiene ninguna relación confiable con la
            // posición/tamaño real -- daba una posición absurda (miles de unidades de distancia).
            // Volvemos a Renderer.bounds (AABB de MUNDO), que Unity SIEMPRE calcula bien sin
            // importar el batching -- es la única fuente confiable acá. El motivo original del
            // cambio (se inflaba con la rotación) no aplica en la práctica: el objeto real casi no
            // está rotado (pocos grados), así que la inflación es despreciable.
            Bounds wb = rend.bounds;
            Vector3 c = wb.center;
            Vector3 s = wb.size;

            // owner: "sos estupido o que" -- el bloque salía deformado (ejes permutados) porque
            // mezclaba 's' (tamaño en ejes de MUNDO) con una rotación NO alineada a los ejes
            // (sel.transform.rotation, que para este objeto combinado ni siquiera es confiable,
            // ver arriba). Escalar en espacio LOCAL rotado usando un tamaño calculado en espacio de
            // MUNDO es matemáticamente incoherente salvo que la rotación sea 0/90/180/270°. Fix:
            // todo en ejes de MUNDO -- el "Plank" queda SIN rotación propia (alineado a mundo), así
            // 's' (AABB de mundo) es directamente su escala local, sin mezclar espacios.
            bool longX = s.x >= s.z;
            float length = longX ? s.x : s.z;
            Vector3 longDir = longX ? Vector3.right : Vector3.forward;   // eje de MUNDO, coherente con 's'
            Vector3 hinge = c - longDir * (length * 0.5f);   // bisagra en un EXTREMO

            // owner: "no se está poniendo el material original" (sigue negro incluso después de
            // NappinUrp -- ese material tiene algo raro que la conversión estándar no arregla).
            // En vez de perseguirlo, uso directo el material de madera YA PROBADO del proyecto
            // (el mismo que usan las vallas de los caminos, FenceBuilder.cs) -- URP/Lit simple,
            // sin sorpresas.
            var fenceTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ExternalAssets/WoodenFence/textures/low_wooden_wall.jpg");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (fenceTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", fenceTex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            mat = BuilderUtils.SaveMaterialStable(mat, "Assets/Settings/WoodenFence.mat");

            var prev = FindByName(name);
            if (prev != null) Object.DestroyImmediate(prev.gameObject);

            var pivot = new GameObject(name);
            pivot.transform.position = hinge;
            pivot.transform.rotation = Quaternion.identity;

            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Plank";
            plank.transform.SetParent(pivot.transform, true);
            plank.transform.position = c;
            plank.transform.rotation = Quaternion.identity;   // alineado a MUNDO -- coherente con 's' (AABB de mundo)
            plank.transform.localScale = s;
            if (mat != null) plank.GetComponent<Renderer>().sharedMaterial = mat;

            var gate = pivot.AddComponent<FolkloreArchives.CorralGate>();
            gate.openDeg = openDeg;
            gate.hintClosed = hintClosed;
            gate.hintOpen = hintOpen;

            sel.SetActive(false);   // ocultamos la puerta combined original

            Undo.RegisterCreatedObjectUndo(pivot, "Armar " + name);
            Selection.activeGameObject = pivot;
            EditorGUIUtility.PingObject(pivot);
            return pivot;
        }

        // owner: el intento con el modelo real (wooden_fence_closed) dio demasiadas vueltas
        // (Static, rotación, pivote) sin llegar a verse -- REVERTIDO al cubo simple
        // (BuildHingeDoor), que sí funciona de forma confiable (aunque sin relieve de tablones).
        // Si más adelante se quiere retomar el asset real, la versión que se probó queda en el
        // historial de git (buscar "wooden_fence_closed" en los commits de este archivo).
        [MenuItem("Folklore/Armar tranquera del corral (abrible)")]
        static void BuildGate()
        {
            var sel = Selection.activeGameObject;
            if (sel == null) { EditorUtility.DisplayDialog("Tranquera", "Seleccioná primero la puerta del corral (Cube.184).", "OK"); return; }
            if (sel.GetComponent<Renderer>() == null) { EditorUtility.DisplayDialog("Tranquera", "El objeto seleccionado no tiene Renderer.", "OK"); return; }
            var pivot = BuildHingeDoor(sel, "TranqueraCorral", 95f, "[E] Abrir la tranquera", "[E] Cerrar la tranquera");
            if (pivot == null) return;
            Debug.Log("[Rancho] 'TranqueraCorral' armada (cubo simple, bisagra en un extremo). Original " +
                      sel.name + " desactivado. Ajustá openDeg (+/-) si abre para el lado equivocado.");
        }

        // owner: "no me está saliendo la opción [de abrir la tranquera]" -- a TranqueraCorral se le
        // había perdido el componente CorralGate (mismo síntoma que ya tuvo PuertaCasa). Sin él, la
        // misión (RanchoBathroomScene) salta directo al hint de después sin esperar a que se abra.
        // Mismo botón "reponer config sin rehacer" que ya existe para la puerta de la casa.
        [MenuItem("Folklore/Actualizar tranquera del corral (config, sin rehacerla)")]
        static void UpdateGateConfig()
        {
            var door = FindByName("TranqueraCorral");
            if (door == null)
            {
                EditorUtility.DisplayDialog("Tranquera", "No encontré 'TranqueraCorral' en la escena -- primero corré 'Armar tranquera del corral (abrible)'.", "OK");
                return;
            }
            var gate = door.GetComponent<FolkloreArchives.CorralGate>();
            bool wasMissing = gate == null;
            if (gate == null) gate = door.gameObject.AddComponent<FolkloreArchives.CorralGate>();
            gate.openDeg = 95f;
            gate.hintClosed = "[E] Abrir la tranquera";
            gate.hintOpen = "[E] Cerrar la tranquera";
            EditorUtility.SetDirty(gate);
            Selection.activeGameObject = door.gameObject;
            EditorGUIUtility.PingObject(door);
            Debug.Log("[Rancho] 'TranqueraCorral' actualizada." +
                      (wasMissing ? " Le faltaba el CorralGate -- se le agregó de nuevo." : "") +
                      " Rotación NO tocada.");
        }

        // owner: "la puerta debería estar cerrada cuando voy a golpearla y también debería poder
        // abrir y cerrarse" -- v1 (BuildHingeDoor con un Cube genérico, como la tranquera) salió
        // FEA: el Cube toma el material con UVs de caja (se veía negra) y el AABB copiaba el
        // bounds del original YA abierto. Mejor: "Door04_pr" tiene un PREFAB PROPIO en el pack
        // ("Assets/ALP_Assets/country house01/Prefabs/Door04_pr.prefab") con la malla real +
        // picaporte + colliders, y el AUTOR ya puso el pivote (0,0,0 local) en la BISAGRA (no en
        // el centro) -- pensado justamente para poder rotarlo. En vez de aproximar con un cubo,
        // se reinstancia ESE prefab fresco (sin static-batch) en el mismo lugar del original y se
        // le cuelga CorralGate directo -- misma malla y textura que el original, sin cubo.
        const string HouseDoorPrefabPath = "Assets/ALP_Assets/country house01/Prefabs/Door04_pr.prefab";

        [MenuItem("Folklore/Armar puerta de la casa (abrible)")]
        static void BuildHouseDoor()
        {
            Debug.Log("[Rancho] BuildHouseDoor: arrancó."); // marca de diagnóstico -- si no ves esto en la Console, el botón no está ejecutando este método.
            var old = Selection.activeGameObject;
            if (old == null) { EditorUtility.DisplayDialog("Puerta", "Seleccioná primero la puerta de la casa (Door04_pr) en la Hierarchy.", "OK"); return; }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HouseDoorPrefabPath);
            if (prefab == null) { EditorUtility.DisplayDialog("Puerta", "No encontré " + HouseDoorPrefabPath, "OK"); return; }

            // transform MUNDO del viejo (ahí la dejamos) -- mismo criterio que LetrinaFixer.
            var ot = old.transform;
            Vector3 wp = ot.position; Quaternion wr = ot.rotation; Vector3 ws = ot.lossyScale;

            var prev = FindByName("PuertaCasa");
            if (prev != null) Object.DestroyImmediate(prev.gameObject);

            var fresh = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            fresh.name = "PuertaCasa";
            PrefabUtility.UnpackPrefabInstance(fresh, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            fresh.transform.position = wp;
            fresh.transform.rotation = wr;
            fresh.transform.localScale = ws;

            // el prefab autoral trae m_StaticEditorFlags=31 (Batching Static incluido) -- si lo
            // dejamos así, Unity la vuelve a static-batchear al entrar a Play y quedamos EXACTO en
            // el mismo problema (Combined Mesh, no rotable). Sacar el flag en TODA la jerarquía.
            foreach (var t in fresh.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)0);

            // el material del prefab tal cual viene del pack (Standard) sale MAGENTA en URP -- el
            // resto de la casa lo convierte al vuelo con HouseBuilder.NappinUrp (cacheado por
            // material -> mismo "nap_DoorsMap01" que ya usa el resto de la casa, si Generate ya
            // corrió en esta sesión del Editor).
            foreach (var r in fresh.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials;
                for (int i = 0; i < src.Length; i++) src[i] = HouseBuilder.NappinUrp(src[i]);
                r.sharedMaterials = src;
            }

            var gate = fresh.AddComponent<FolkloreArchives.CorralGate>();
            ApplyHouseDoorConfig(gate);

            old.SetActive(false);   // la puerta combined original queda desactivada

            Undo.RegisterCreatedObjectUndo(fresh, "Armar puerta de la casa");
            Selection.activeGameObject = fresh;
            EditorGUIUtility.PingObject(fresh);
            Debug.Log("[Rancho] 'PuertaCasa' repuesta con el PREFAB real (Door04_pr: malla + picaporte + " +
                      "colliders) en " + wp + ". Original " + old.name + " desactivado. Arranca CERRADA " +
                      "con la pose que tenga al entrar a Play -- el pivote del prefab YA está en la " +
                      "bisagra (autoral), así que si se ve entreabierta simplemente rotá 'PuertaCasa' en " +
                      "el Editor hasta que cierre bien, ANTES de dar Play. Ajustá openDeg (+/-) en el " +
                      "CorralGate si abre para el lado equivocado.");
        }

        // valores de config de la puerta de la casa (hint/lado que abre/sonido) -- separado de
        // BuildHouseDoor para poder reaplicarlos SIN reconstruir el objeto (ver botón de abajo).
        static void ApplyHouseDoorConfig(FolkloreArchives.CorralGate gate)
        {
            gate.hintClosed = "[E] Abrir la puerta";
            gate.hintOpen   = "[E] Cerrar la puerta";
            gate.openDeg = -100f;   // owner: abría para ADENTRO -- invertido para que abra hacia afuera
            gate.openClipName  = "door_open";    // owner: nada de sonido de puerta de auto -- puerta normal
            gate.closeClipName = "door_close";
        }

        // owner: "no me aplicaste los cambios... sigue abriendo para adentro, sigue el ruido del
        // auto, sigue diciendo tranquera" -- ese CorralGate ya estaba CREADO en la escena desde
        // antes; cambiar los valores por default en el código NO actualiza un componente que ya
        // existe (solo aplican a una instancia NUEVA). Este botón reaplica hint/openDeg/sonido a la
        // "PuertaCasa" que YA está en la escena, SIN tocar su rotación (la "cerrada" ajustada a mano).
        [MenuItem("Folklore/Actualizar puerta de la casa (config, sin rehacerla)")]
        static void UpdateHouseDoorConfig()
        {
            var door = FindByName("PuertaCasa");
            if (door == null)
            {
                EditorUtility.DisplayDialog("Puerta", "No encontré 'PuertaCasa' en la escena -- primero corré 'Armar puerta de la casa (abrible)'.", "OK");
                return;
            }
            // owner: le faltaba el CorralGate por completo (no se sabe por qué -- puede haberse
            // quitado a mano sin querer, o algún Undo). Antes esto solo avisaba con un diálogo y
            // no arreglaba nada; ahora lo AGREGA si falta, en vez de fallar.
            var gate = door.GetComponent<FolkloreArchives.CorralGate>();
            bool wasMissing = gate == null;
            if (gate == null) gate = door.gameObject.AddComponent<FolkloreArchives.CorralGate>();
            ApplyHouseDoorConfig(gate);
            EditorUtility.SetDirty(gate);
            Selection.activeGameObject = door.gameObject;
            EditorGUIUtility.PingObject(door);
            Debug.Log("[Rancho] 'PuertaCasa' actualizada (hint, lado que abre, sonido)." +
                      (wasMissing ? " Le faltaba el CorralGate -- se le agregó de nuevo." : "") +
                      " Rotación NO tocada.");
        }

        // owner: quiso acomodar "letrina.007" (la puerta del baño del rancho) y se le rompía la
        // malla al rotarla -- mismo síntoma de siempre (Combined Mesh). Solución: correr primero
        // "Reponer letrina (fresca con texturas)" (LetrinaFixer), que la deja con SU PROPIA malla
        // (ya no combinada). Una vez limpia, hacerla abrible es directo: no hace falta reconstruir
        // nada (a diferencia de la puerta de la casa), solo colgarle CorralGate.
        [MenuItem("Folklore/Armar puerta de la letrina (abrible)")]
        static void BuildLetrinaDoor()
        {
            var sel = Selection.activeGameObject;
            if (sel == null) { EditorUtility.DisplayDialog("Letrina", "Seleccioná primero la puerta de la letrina (letrina.007) en la Hierarchy.", "OK"); return; }
            if (sel.GetComponent<Renderer>() == null) { EditorUtility.DisplayDialog("Letrina", "El objeto seleccionado no tiene Renderer.", "OK"); return; }

            var gate = sel.GetComponent<FolkloreArchives.CorralGate>();
            if (gate == null) gate = Undo.AddComponent<FolkloreArchives.CorralGate>(sel);
            gate.hintClosed = "[E] Abrir la puerta";
            gate.hintOpen   = "[E] Cerrar la puerta";
            gate.openDeg = -100f;   // mismo criterio que la puerta de la casa: para afuera
            gate.openClipName  = "door_open";
            gate.closeClipName = "door_close";
            EditorUtility.SetDirty(sel);

            Selection.activeGameObject = sel;
            EditorGUIUtility.PingObject(sel);
            Debug.Log("[Rancho] '" + sel.name + "' ahora es abrible (CorralGate). Arranca CERRADA con " +
                      "la pose que tenga al entrar a Play -- dejala así en el Editor si ya se ve bien " +
                      "cerrada. Ajustá openDeg (+/-) en el Inspector si abre para el lado equivocado. " +
                      "(La secuencia del susto ya desactiva/reactiva este CorralGate sola alrededor del " +
                      "golpe scripteado, para que esa E no la abra.)");
        }

        // pone N ovejas (sheep.obj) en un cluster cerca de la tranquera. La secuencia las
        // mueve al pastizal cuando abrís la tranquera. Grupo "Ovejas" con hijos "Oveja_i".
        [MenuItem("Folklore/Poner ovejas en el corral")]
        static void PlaceSheep()
        {
            AssetDatabase.Refresh();
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(SheepObj);
            if (obj == null) { EditorUtility.DisplayDialog("Ovejas", "No encontré " + SheepObj + " (¿lo importó Unity?).", "OK"); return; }

            var prev = FindByName("Ovejas");
            if (prev != null) Object.DestroyImmediate(prev.gameObject);
            var group = new GameObject("Ovejas");

            // punto de spawn del rebaño (owner TEST_PLAYER, adentro del corral)
            Vector3 baseP = new Vector3(108.905f, 26.75688f, 154.3876f);

            // material URP con la textura (Point = PSX)
            var tex = LoadPointTex(SheepTex);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            mat = BuilderUtils.SaveMaterialStable(mat, "Assets/Settings/PSX_Oveja.mat");

            const int N = 4;
            for (int i = 0; i < N; i++)
            {
                var s = BuildSheep(obj, mat, i);
                s.transform.SetParent(group.transform);
                float ang = i * 90f * Mathf.Deg2Rad;
                Vector3 p = baseP + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (1.0f + i * 0.25f);
                if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 30f)) p.y = hit.point.y;
                s.transform.position = p;
                s.transform.rotation = Quaternion.Euler(0f, i * 63f, 0f);
            }
            Undo.RegisterCreatedObjectUndo(group, "Poner ovejas");
            Selection.activeGameObject = group;
            EditorGUIUtility.PingObject(group);
            Debug.Log("[Rancho] " + N + " ovejas puestas en 'Ovejas' cerca del corral. Movelas si hace falta.");
        }

        static GameObject BuildSheep(GameObject obj, Material mat, int idx)
        {
            var go = new GameObject("Oveja_" + idx);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(obj);
            model.name = "Model";
            model.transform.SetParent(go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float h = Mathf.Max(0.0001f, b.size.y);
                model.transform.localScale = Vector3.one * (SheepHeight / h);
                Bounds b2 = rends[0].bounds;
                foreach (var r in model.GetComponentsInChildren<Renderer>()) b2.Encapsulate(r.bounds);
                model.transform.localPosition = new Vector3(0f, -(b2.min.y - go.transform.position.y), 0f);
                foreach (var r in rends)
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                    r.sharedMaterials = arr;
                }
            }
            var col = go.AddComponent<CapsuleCollider>();
            col.height = SheepHeight; col.radius = SheepHeight * 0.4f; col.center = new Vector3(0f, SheepHeight * 0.5f, 0f);
            return go;
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

        static Transform FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == name) return t;
            return null;
        }
    }
}
