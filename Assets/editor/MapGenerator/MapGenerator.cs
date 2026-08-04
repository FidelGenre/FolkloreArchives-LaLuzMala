// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  MapGenerator.cs — entry point. Adds the menu:
//  Tools > Folklore Archives > Generate Greybox Map
//  Paste into:  Assets/Editor/MapGenerator/MapGenerator.cs
// ============================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FolkloreArchives.MapGen
{
    // Dibuja un botón fijo en el corner superior-derecho del Scene View para
    // cambiar entre día y noche sin ir al menú. Se registra automáticamente al
    // abrir Unity (InitializeOnLoad).
    [InitializeOnLoad]
    public static class DayNightSceneButton
    {
        static bool _isDay = false;

        static DayNightSceneButton()
        {
            SceneView.duringSceneGui += Draw;
        }

        static void Draw(SceneView sv)
        {
            // Solo mostrar si el mapa está generado
            if (GameObject.Find(MapLayout.RootName) == null) return;

            Handles.BeginGUI();
            float w = 140f, h = 28f, margin = 8f;
            var rect = new Rect(sv.position.width - w - margin, margin, w, h);

            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            string label = _isDay ? "☽  Pasar a Noche" : "☀  Pasar a Día";
            if (GUI.Button(rect, label, style))
            {
                MapGenerator.ToggleDayNight();
                _isDay = !_isDay;
            }

            // Second button below: fog on/off (mirrors the day/night one)
            var fogRect = new Rect(sv.position.width - w - margin, margin + h + 4f, w, h);
            string fogLabel = RenderSettings.fog ? "🌫  Niebla: ON" : "🌫  Niebla: OFF";
            if (GUI.Button(fogRect, fogLabel, style))
                MapGenerator.ToggleFog();

            Handles.EndGUI();
        }
    }

    public static class MapGenerator
    {
        [MenuItem("Tools/Folklore Archives/Generate Greybox Map")]
        public static void Generate()
        {
            Random.InitState(MapLayout.Seed);
            BuilderUtils.EnsureFolders();
            DeleteMap();

            var root = new GameObject(MapLayout.RootName);

            // Cronómetro por fase → así se ve en la consola qué parte se come el tiempo.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long prev = 0;
            void Lap(string name) { long now = sw.ElapsedMilliseconds; Debug.Log($"[GEN] {name}: {(now - prev) / 1000f:0.0}s"); prev = now; }

            SkyboxMountainBaker.BakeCached();   // solo hornea si falta (ahorra tiempo en cada Generate)
            Lap("Skybox");

            Terrain terrain = TerrainBuilder.Build(root.transform);            Lap("Terrain");
            EnvironmentBuilder.Build(root.transform);
            EnvironmentBuilder.BuildDaySky(); // pre-genera mat_daysky.mat para el DayNightController
            Lap("Environment");
            ForestBuilder.Build(root.transform, terrain);                      Lap("Forest (arboles+pasto)");
            RoadsideBuilder.Build(root.transform, terrain); // guardrail + lake on the road's south side
            BridgeBuilder.Build(root.transform, terrain);   // steel-girder bridge over the water crossing
            PedestrianBridgeBuilder.Build(root.transform, terrain); // puente colgante peatonal (cruce del campamento)
            TunnelBuilder.Build(root.transform, terrain);   // west-end drivable tunnel (game start)
            LandmarkBuilder.Build(root.transform, terrain);                    Lap("Roadside+Bridge+Tunnel+Landmark+Campamento");
            // Montañas de fondo: desactivadas por ahora. El método de "cámara de fondo"
            // rompía el skybox/día-noche en URP. El camino correcto es un SKYBOX con
            // montañas (mantiene cielo + montañas, funciona con niebla, sin 2ª cámara).
            // SilhouetteMountainBuilder.Build(root.transform);
            // MountainRingBuilder.Build(root.transform, terrain); // anillo LEJANO (desactivado, rompía skybox con cámara de fondo)
            // DESACTIVADO DE NUEVO (owner: "no me gustan para nada esas montañas, quitalas") — después
            // de 4 rondas de ajuste (escala, radio de roca, empuje fuera del agua) el asset HQP en
            // sí no convenció. Queda el bulto procedural del terreno (CentralPeakHeight en
            // TerrainBuilder) nomás, sin el mesh, hasta que el owner baje un asset de montaña nuevo
            // (ver charla: Free Snow Mountain / Low Poly Mountains Environment / etc.) y pida
            // reactivar esto apuntando a los prefabs nuevos.
            // MountainRingBuilder.BuildCentralLakeMountains(root.transform, terrain);
            AreaPoiBuilder.Build(root.transform, terrain);   // zonas/POIs nuevos del MapPlan (estepa, mallín, roquedal, quemado, orilla, Difunta Correa, Gauchito Gil, ahorcado, antena, corrales, YPF, estancia)
            HouseBuilder.Build(root.transform, terrain);     // casa de la vieja (OldLadyRanch) — Fase 1: cáscara + valla
            OldLadyNpcBuilder.Build(root.transform, terrain); // la vieja cuentacuentos, parada afuera de su casa
            FenceBuilder.Build(root.transform, terrain);      // valla de madera junto al camino de tierra y al sendero a la casa de la vieja
            var carGO = CarBuilder.Build(root.transform, terrain);       // Renault 12 procedural (auto manejable) — estacionado en el campamento
            // owner: "hace que se puedan sentar no mas en el auto decorativos" -- corre
            // DESPUÉS de CarBuilder (los amigos ya están parados por LandmarkBuilder,
            // pero el auto recién existe acá) para sentarlos en los 3 asientos libres.
            FriendNpcBuilder.SeatInCar(root.transform, carGO.GetComponent<FolkloreArchives.CarController>());
            LuzMalaBuilder.Build(root.transform, terrain);   // La Luz Mala (aparece de noche)
            StoryTriggerBuilder.Build(root.transform, terrain);
            TestPlayerBuilder.Build(root.transform, terrain);

            // owner: "vamos todos en el auto desde el inicio de mapa hasta la
            // gasolinera" -- arma la secuencia de apertura (auto maneja solo, jugador+
            // perro sentados atrás desde el arranque, parada en YPF, reaparición de
            // los 3 amigos después). Corre DESPUÉS de TestPlayerBuilder (necesita
            // TEST_PLAYER/DOG ya armados) y de FriendNpcBuilder.SeatInCar (necesita los
            // 3 amigos ya reparentados bajo el auto).
            var testPlayerGO = GameObject.Find("TEST_PLAYER");
            var dogGO = GameObject.Find("DOG");
            if (testPlayerGO != null && dogGO != null)
            {
                var seq = carGO.AddComponent<FolkloreArchives.OpeningDriveSequence>();
                seq.car = carGO.GetComponent<FolkloreArchives.CarController>();
                seq.autoDrive = carGO.GetComponent<FolkloreArchives.CarAutoDrive>();
                seq.carDoors = carGO.GetComponent<FolkloreArchives.Net.CarDoors>();
                seq.player = testPlayerGO.GetComponent<FolkloreArchives.PlayerVehicleInteractor>();
                seq.dog = dogGO.GetComponent<FolkloreArchives.PlayerVehicleInteractor>();
                seq.friendMaleCasual = carGO.transform.Find("Friend_MaleCasual");
                seq.friendMaleGreenJkt = carGO.transform.Find("Friend_MaleGreenJkt");
                seq.friendFemaleSec = carGO.transform.Find("Friend_FemaleSec");
            }
            else Debug.LogWarning("[MapGenerator] No encontré TEST_PLAYER/DOG -- OpeningDriveSequence no se armó.");
            Lap("Casa+Story+Player");

            // Red (co-op): NET con NetworkManager + transporte + panel de conexión +
            // prefab de jugador de prueba. Idempotente; persiste entre regenerados.
            NetworkBuilder.EnsureNet();
            Lap("Red (prefabs persona/perro)");

            // Aplica cualquier posición/rotación/escala guardada con
            // Tools > Save Map Layout, DESPUÉS de que todos los builders de arriba
            // ya armaron el mapa desde MapLayout.cs (si no, esto se pisaría).
            MapLayoutPersistence.ApplySavedLayout();
            Lap("Aplicar layout manual guardado");

            // Coser los terrenos VECINOS (el generado + los extra que agregó el owner)
            // para que no se vea la costura entre ellos. Auto-connect de Unity: terrenos
            // adyacentes con el mismo groupingID se unen solos. Se re-aplica cada Generate
            // porque el terreno generado es nuevo cada vez.
            var genData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBuilder.TerrainAssetPath);
            foreach (var terr in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                terr.allowAutoConnect = true;
                // Meter los terrenos EXTRA del owner DENTRO de FOLKLORE_MAP (el generado
                // ya está). El DeleteMap los saca afuera para no borrarlos al regenerar;
                // acá los vuelvo a colgar del mapa nuevo (conserva su posición de mundo).
                if (terr.terrainData != genData && terr.transform.parent != root.transform)
                    terr.transform.SetParent(root.transform, true);
            }
            // owner: no aparecía el menú Tools -- error de compilación (Unity 6:
            // Terrain.SetConnectivityDirty() pasó a ser un método ESTÁTICO, ya no se
            // puede llamar sobre una instancia como antes). Alcanza con llamarlo una
            // vez (no por terreno) para que Unity vuelva a coser todos los adyacentes.
            Terrain.SetConnectivityDirty();
            Lap("Coser terrenos vecinos");

            // Mismo criterio que el terreno extra de arriba: la(s) ruta(s) rescatadas
            // por DeleteMap (PavedRoad_Surface*) quedaron colgadas de la raíz de la
            // escena -- las vuelvo a meter DENTRO de FOLKLORE_MAP (organizadas).
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.StartsWith("PavedRoad_Surface") && t.parent != root.transform)
                    t.SetParent(root.transform, true);
            }

            // owner (companero): "reconstruimos TODA la ruta del auto desde la
            // geometría de la escena, sin coordenadas hardcodeadas" -- tiene que
            // correr ACÁ, DESPUÉS de que las piezas reales de la ruta (arriba) ya
            // estén de vuelta colgadas de root.transform. Antes se llamaba justo
            // después de ApplySavedLayout, TODAVÍA antes de este re-parenteo -- en
            // ese momento las piezas rescatadas por DeleteMap seguían sueltas en la
            // raíz de la escena, así que SnapToRoadExtensionTip no encontraba
            // ninguna (pts.Count < 2), abortaba en silencio, y quedaba vigente la
            // ruta vieja de 21 puntos que arma CarBuilder.Build() (raycast+material,
            // mucho más tosca) -- el auto no seguía bien las curvas reales.
            CarBuilder.SnapToRoadExtensionTip(root.transform);
            Lap("Reubicar auto sobre la ruta real (companero)");

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); // salva el .unity para que el Build incluya el mapa
            Lap("Guardar assets+escena");
            Selection.activeGameObject = root;
            Debug.Log("<color=lime>LA LUZ MALA map generated. Press Play: WASD + mouse, Shift = run, F = flashlight.</color>");
        }

        [MenuItem("Tools/Folklore Archives/Delete Map")]
        public static void DeleteMap()
        {
            var old = GameObject.Find(MapLayout.RootName);
            if (old == null) return;

            // Rescatar terrenos EXTRA agregados a mano por el owner (para extender el
            // mapa): cualquier Terrain bajo el mapa que NO sea el generado (FolkloreTerrain)
            // se re-parentea FUERA, así el Generate no lo borra. Sus ediciones viven en su
            // propio TerrainData → sobreviven al regenerado. Identifico el generado por su
            // asset (no por nombre, porque Unity llama "Terrain" a los nuevos por defecto).
            var genData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBuilder.TerrainAssetPath);
            foreach (var terr in old.GetComponentsInChildren<Terrain>(true))
            {
                if (terr.terrainData == genData) continue;   // el generado se rehace solo
                terr.transform.SetParent(null, true);         // a la raíz de la escena (conserva pos mundo)
                Debug.Log($"<color=cyan>[Generate] Terreno extra '{terr.name}' rescatado fuera del mapa (no se borra).</color>");
            }

            // owner: la ruta pavimentada real (PavedRoad_Surface, y cualquier copia
            // tipo "PavedRoad_Surface (1)") la coloca el compañero a mano/EasyRoads3D,
            // sincronizada por Unity Version Control -- ningún Builder de acá la
            // regenera. El rescate de arriba solo mira componentes Terrain, así que
            // este mesh se colaba sin rescatar y quedaba DESTRUIDO PARA SIEMPRE en
            // cada Generate (CarBuilder.FindRoadTip() nunca la encontraba, el auto
            // caía siempre al sistema procedural viejo). Mismo patrón de rescate.
            foreach (var t in old.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.name.StartsWith("PavedRoad_Surface")) continue;
                t.SetParent(null, true);
                Debug.Log($"<color=cyan>[Generate] Ruta real del compañero '{t.name}' rescatada fuera del mapa (no se borra).</color>");
            }
            Object.DestroyImmediate(old);
        }

        // Switches between DAY (to inspect the map with full light, no fog)
        // and NIGHT (the real game mood). Day is only a preview tool.
        // Shortcut: Ctrl+Shift+D
        [MenuItem("Tools/Folklore Archives/Toggle Day-Night Preview %#d")]
        public static void ToggleDayNight()
        {
            var moon = GameObject.Find("Moon");
            if (moon == null)
            {
                Debug.LogWarning("Generate the map first (Tools > Folklore Archives > Generate Greybox Map).");
                return;
            }
            var light = moon.GetComponent<Light>();
            bool toDay = light.intensity < 0.8f;
            var dnc = Object.FindFirstObjectByType<FolkloreArchives.DayNightController>();
            if (toDay)
            {
                light.intensity = 1.0f;  // un poco menos de sol directo
                light.color = new Color(1f, 0.92f, 0.72f);
                light.shadows = LightShadows.Soft;
                RenderSettings.skybox = EnvironmentBuilder.DaySkybox(); // AllSky si está
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.30f, 0.26f, 0.32f); // más oscuro — menos sobreexpuesto
                RenderSettings.fog              = true;
                RenderSettings.fogMode          = FogMode.Linear;
                RenderSettings.fogStartDistance = MapLayout.DayFogStart;
                RenderSettings.fogEndDistance   = MapLayout.DayFogEnd;
                RenderSettings.fogColor         = MapLayout.DayFogColor;
                var t = Terrain.activeTerrain;
                if (t != null) { t.detailObjectDistance = MapLayout.DayDetailRenderDistance; t.treeDistance = MapLayout.DayTreeRenderDistance; t.detailObjectDensity = 0.20f; }
                ForestBuilder.SetGrassFadeGlobals(MapLayout.DayDetailRenderDistance);
                Shader.SetGlobalColor("_GrassTintMul", MapLayout.GrassDayTint);
                var cam = Camera.main;
                if (cam != null) cam.farClipPlane = MapLayout.DayCameraFarClip;
                if (dnc != null) dnc.SetDay(true);
                Debug.Log("<color=yellow>DAY. Toggle again to restore night.</color>");
            }
            else
            {
                light.intensity = MapLayout.MoonIntensity;
                light.color = new Color(0.42f, 0.52f, 0.78f);
                light.shadows = LightShadows.Hard;
                RenderSettings.skybox = EnvironmentBuilder.NightSkybox(); // AllSky si está
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.016f, 0.026f, 0.052f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = MapLayout.FogDensity;
                RenderSettings.fogColor = new Color(0.035f, 0.055f, 0.105f);
                var t = Terrain.activeTerrain;
                if (t != null) { t.detailObjectDistance = MapLayout.DetailRenderDistance; t.treeDistance = MapLayout.TreeRenderDistance; t.detailObjectDensity = MapLayout.DetailDensity; }
                ForestBuilder.SetGrassFadeGlobals(MapLayout.DetailRenderDistance);
                Shader.SetGlobalColor("_GrassTintMul", Color.white); // noche: sin cambio
                var cam = Camera.main;
                if (cam != null) cam.farClipPlane = MapLayout.CameraFarClip;
                if (dnc != null) dnc.SetDay(false);
                Debug.Log("<color=cyan>NIGHT restored.</color>");
            }
        }

        // Toggles scene fog on/off, for inspecting the map without the murk
        // (mirrors the day/night preview). Only flips RenderSettings.fog; the
        // density/color/mode are left as the current day or night preset, so
        // turning fog back ON restores whatever mood was active.
        // Shortcut: Ctrl+Shift+F
        [MenuItem("Tools/Folklore Archives/Toggle Fog %#f")]
        public static void ToggleFog()
        {
            RenderSettings.fog = !RenderSettings.fog;
            Debug.Log(RenderSettings.fog
                ? "<color=cyan>Fog ON.</color>"
                : "<color=yellow>Fog OFF — toggle again to restore.</color>");
            SceneView.RepaintAll();
        }

        // owner: "no puedo manejar el auto para probar la ruta nueva, arranca la
        // secuencia sola" -- toggle de TESTING para saltar OpeningDriveSequence
        // (auto-sentarse + manejar solo hasta la YPF) y poder subirse/manejar a
        // mano con WASD, para anotar el trazado real de la ruta nueva.
        [MenuItem("Tools/Folklore Archives/Debug: Saltar Secuencia Auto (Testing)")]
        public static void ToggleSkipAutoDriveSequence()
        {
            FolkloreArchives.OpeningDriveSequence.SkipForTesting = !FolkloreArchives.OpeningDriveSequence.SkipForTesting;
            Debug.Log(FolkloreArchives.OpeningDriveSequence.SkipForTesting
                ? "<color=yellow>[Debug] Secuencia de auto SALTEADA -- al dar Play podés manejar a mano.</color>"
                : "<color=cyan>[Debug] Secuencia de auto normal restaurada.</color>");
        }

        // owner: TraceRoadPath (CarBuilder.cs) siempre da "981.9m" al vértice más
        // cercano al spawn, sin importar el fix -- señal de que NINGÚN
        // "PavedRoad_Surface*" está cerca de verdad. Encontrado por separado: hay
        // 70+ objetos con ese nombre en la escena (acumulados, probablemente de las
        // rondas de merge/conflictos de Unity Version Control de la sesión). Esta
        // herramienta agrupa esos 70+ por posición (cada 5m) para ver cuántos
        // LUGARES distintos hay de verdad (vs. copias apiladas en el mismo sitio) y
        // el bounding box combinado de todos -- para saber de una vez si el camino
        // real cerca del spawn tiene ESTE nombre o es otra cosa.
        [MenuItem("Tools/Folklore Archives/Debug: Listar PavedRoad_Surface")]
        public static void DebugListPavedRoadSurfaces()
        {
            var matches = new System.Collections.Generic.List<Transform>();
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (tr.name.StartsWith("PavedRoad_Surface")) matches.Add(tr);

            Debug.Log($"<color=yellow>[Debug] {matches.Count} objetos 'PavedRoad_Surface*' encontrados.</color>");

            var clusters = new System.Collections.Generic.Dictionary<Vector3Int, int>();
            Bounds? totalBounds = null;
            foreach (var tr in matches)
            {
                var key = new Vector3Int(Mathf.RoundToInt(tr.position.x / 5f), Mathf.RoundToInt(tr.position.y / 5f), Mathf.RoundToInt(tr.position.z / 5f));
                clusters.TryGetValue(key, out int c);
                clusters[key] = c + 1;

                var rend = tr.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    if (totalBounds == null) totalBounds = rend.bounds;
                    else { var tb = totalBounds.Value; tb.Encapsulate(rend.bounds); totalBounds = tb; }
                }
            }
            Debug.Log($"<color=yellow>[Debug] {clusters.Count} posiciones distintas (agrupadas cada 5m) entre esos {matches.Count} objetos.</color>");
            foreach (var kv in clusters)
                Debug.Log($"<color=yellow>[Debug]   cluster ~({kv.Key.x * 5}, {kv.Key.y * 5}, {kv.Key.z * 5}) x{kv.Value}</color>");
            if (totalBounds.HasValue)
                Debug.Log($"<color=yellow>[Debug] Bounds combinado de TODOS: min={totalBounds.Value.min} max={totalBounds.Value.max}</color>");
        }

        // owner: "sigue igual yendose para la derecha" -- pese a la corrección en
        // vivo (CarAutoDrive.IsOnAsphalt/FindNearestAsphalt). Sospecha: entre los
        // 70+ "PavedRoad_Surface*" acumulados hay restos con collider en lugares
        // raros (fuera de la ruta real) que pueden estar dando falsos positivos de
        // "esto es asfalto" a esos raycasts de corrección, o directamente
        // empujando al auto por colisión. Esta herramienta deja UN SOLO objeto por
        // cada posición real distinta (agrupada cada 5m, mismo criterio que
        // "Debug: Listar") -- el que tenga malla+collider de verdad -- y borra
        // TODOS los demás (duplicados exactos y restos rotos sin malla).
        [MenuItem("Tools/Folklore Archives/Debug: Limpiar PavedRoad_Surface Duplicados")]
        public static void DebugCleanupPavedRoadSurfaces()
        {
            var matches = new System.Collections.Generic.List<Transform>();
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (tr.name.StartsWith("PavedRoad_Surface")) matches.Add(tr);

            var keptPerCluster = new System.Collections.Generic.Dictionary<Vector3Int, Transform>();
            var toDelete = new System.Collections.Generic.List<Transform>();
            foreach (var tr in matches)
            {
                var key = new Vector3Int(Mathf.RoundToInt(tr.position.x / 5f), Mathf.RoundToInt(tr.position.y / 5f), Mathf.RoundToInt(tr.position.z / 5f));
                var mf = tr.GetComponent<MeshFilter>();
                bool hasRealMesh = mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0;

                if (!hasRealMesh) { toDelete.Add(tr); continue; }

                if (!keptPerCluster.TryGetValue(key, out var kept))
                {
                    keptPerCluster[key] = tr; // primero con malla real en este lugar -- se queda
                }
                else
                {
                    toDelete.Add(tr); // ya hay uno bueno guardado para este lugar -- de más
                }
            }

            foreach (var tr in toDelete)
                if (tr != null) Object.DestroyImmediate(tr.gameObject);

            Debug.Log($"<color=lime>[Debug] Limpieza PavedRoad_Surface: {matches.Count} encontrados, " +
                      $"{keptPerCluster.Count} conservados (uno por lugar real), {toDelete.Count} borrados " +
                      $"(duplicados o sin malla real).</color>");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }
}
