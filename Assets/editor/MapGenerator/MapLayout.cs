// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  MapLayout.cs — ALL map data in one place (locations, paths, tuning).
//  Edit this file to move things, then regenerate the map.
//  Paste into:  Assets/Editor/MapGenerator/MapLayout.cs
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class MapLayout
    {
        // ------------- General -------------
        public const int Seed = 20260704;          // change for a different tree distribution
        // The map is now LONGER in x than in z, so the paved road has a long approach
        // before it reaches the inhabited area. `MapSize` keeps its old meaning (the z
        // extent) since most z-axis code already uses it; `MapSizeX` is the x extent.
        public const float MapSize  = 1000f;       // z extent (metres)
        public const float MapSizeX = 1400f;       // x extent (metres) - the extra 400m is road approach on the east
        public const float MaxHeight = 135f;       // subido para picos nevados altos (antes 60)
        public const string RootName = "FOLKLORE_MAP";
        public const string GeneratedFolder = "Assets/_FolkloreArchives/Generated";
        public const string ForestPackFolder = "Assets/ExternalAssets/ForestPack"; // real ground/rock/grass assets (trees no longer used from here - see ALanTreeFolder)
        public const string ALanTreeFolder = "Assets/ExternalAssets/ALanTree"; // single lightweight tree (~1.2MB fbx) - replaces ForestPack's 88k-tri tree
        public const string NatureKitFolder = "Assets/NatureStarterKit2/Textures"; // Nature Starter Kit 2 (Asset Store)

        // EXPERIMENTO (owner): usar los pinos low-poly de Polytope Studio en vez de los
        // Conifers BOTD. REVERTIR: poné false (vuelve a BOTD). Ojo: low-poly no tiene
        // billboards/LOD/viento, así que chocará con el look realista y puede pesar más.
        public const bool UseLowPolyTrees = true;

        // PSX (StarkCrafts): usar los árboles PSX_Tree1..4 del FBX como árboles del
        // bosque (look Fears-to-Fathom). Si el FBX no está importado, cae a low-poly/BOTD.
        // El pasto/detalles siguen con Polytope por ahora.
        // PSX (StarkCrafts): usar los árboles PSX_Tree1..4 del FBX como árboles del
        // bosque (look Fears-to-Fathom). Si el FBX no está importado, cae a low-poly/BOTD.
        // El pasto/detalles siguen con Polytope por ahora.
        public const bool UsePsxTrees = true;

        // PSX grass: usa la textura PSX_Grass_128px (extraída del FBX de StarkCrafts)
        // como DETALLE DE TEXTURA (grass billboard) en vez de las mallas instanciadas
        // de Polytope. Mucho más liviano (el pasto era lo más caro del terreno) y pega
        // con los pinos PSX. Poné false para volver al pasto low-poly de mallas.
        public const bool UsePsxGrass = true;

        // PSX ground: las 7 texturas seamless de 128px que trae el pack de StarkCrafts
        // (PSX_ForestGround_Tex) como capas del terreno, con filtro Point → pixelado PS1.
        // El asfalto de la ruta y la nieve de los picos NO se tocan (el pack no los trae).
        public const bool UsePsxGround = true;

        // ------------- Key locations (x, z) — matches the hand-drawn plan -------------
        // (DirtTurnoff is derived from the smooth paved route further down, so it always
        //  sits exactly on the road wherever the road is at x=620.)
        // Coordenadas del PLANO FINAL del owner (MapPlan.html). Layout de dos lados:
        // OESTE = humano, ESTE = peligro, río al medio (~x595).
        public static readonly Vector2 Campsite         = new Vector2(410, 442);  // campamento (oeste, cerca del río)
        public static readonly Vector2 OldLadyRanch     = new Vector2(398, 625);  // "VIEJA"
        public static readonly Vector2 HuntingField     = new Vector2(165, 397);  // "CAMPO DE CAZA" (oeste)
        public static readonly Vector2 Grave            = new Vector2(775, 475);  // "TUMBA" — este, frente al Mirador Este
        public static readonly Vector2 MainCriminalCamp = new Vector2(905, 395);  // "DELINCUENTES PRINCIPAL" (este)
        public static readonly Vector2 SecondaryCamp    = new Vector2(942, 597);  // "CAMPAMENTO SECUNDARIO" (este)
        public static readonly Vector2 HostageArea      = new Vector2(917, 495);  // "REHENES" (este)
        public static readonly Vector2 LakeMountain     = new Vector2(135, 645);  // "MONTAÑA Y LAGO" — lago (oeste lejano)
        public static readonly Vector2 WrongTurnDeath   = new Vector2(186, 222);  // "MUERTE CAMINO EQUIVOCADO" (oeste, spur)
        public static readonly Vector2 LakeLookout      = new Vector2(270, 530);  // "MIRADOR OESTE" (centro del cuarteto oeste)
        public static readonly Vector2 AbandonedCabin   = new Vector2(373, 253);  // "CABAÑA OESTE" — antes del campamento
        // Zonas NUEVAS del plano de dos lados:
        public static readonly Vector2 EscapePoint      = new Vector2(803, 76);   // "ESCAPE" — sobre la ruta (z de la ruta a x803)
        public static readonly Vector2 CabinEast        = new Vector2(683, 327);  // "CABAÑA ESTE" — bajando de la Tumba al Escape
        public static readonly Vector2 LookoutEast      = new Vector2(698, 609);  // "MIRADOR ESTE" — orilla este del río

        // ------------- Lote de la casa de la vieja (HouseBuilder) -------------
        // Bounds en world XZ del cerco (deben coincidir con la valla de HouseBuilder:
        // grupo en OldLadyRanch-(8,7) [casa en L, bounding 16×14], valla local
        // x[-6..30] z[-7..19] → world abajo). TerrainBuilder aplana este rectángulo a
        // OldLadyLotHeight y ForestBuilder no pone árboles adentro (+ un margen).
        // Ajustar si cambia la valla o el centrado de la casa.
        public static readonly Vector2 OldLadyLotMin = new Vector2(384f, 611f);
        public static readonly Vector2 OldLadyLotMax = new Vector2(420f, 637f);
        public const float OldLadyLotHeight = 25.5f;   // nivel plano del lote (≈ altura natural ahí)

        // ------------- Paths (polylines, x/z) -------------
        // The paved route is a smooth Catmull-Rom curve through a handful of gentle,
        // widely-spaced control points (NOT the old up/down/up/down zig-zag). Long
        // wavelength + small amplitude = a believable rural road with sweeping bends.
        // Control points run past both map edges (x < 0 and x > MapSize) so the road
        // enters and leaves the terrain mid-curve instead of ending square-on.
        static readonly Vector2[] PavedControls = {
            new Vector2(-260, 86),
            new Vector2(150,  70),
            new Vector2(520,  92),
            new Vector2(880,  72),
            new Vector2(1180, 90),
            new Vector2(1500, 74),   // long approach across the extended east half of the map
            new Vector2(1660, 82)
        };
        // Sampled into a fine polyline (~22m spacing) so it reads as a true curve, not
        // a set of straights with kinks. Stays x-monotonic, so PavedRouteZAt still works.
        public static readonly Vector2[] PavedRoute = BuildSmoothRoute(PavedControls, 22f);

        // Where the dirt road leaves the paved route - kept exactly on the curve.
        public static readonly Vector2 DirtTurnoff = new Vector2(331f, PavedRouteZAt(331f)); // desvío del plano (oeste)
        // Caminos que salen del campamento: ahora en S (curvas suaves Catmull-Rom con
        // puntos que zigzaguean) en vez de líneas rectas (pedido del owner).
        public static readonly Vector2[] DirtRoad   = Snake(new[] { DirtTurnoff, new Vector2(346, 251), Campsite }, 14f, 8f); // ruta de tierra en S
        // PathA = sendero VERDE (oeste, frondoso): Montaña y Lago → Vieja → Campamento.
        public static readonly Vector2[] PathA      = Snake(new[] { LakeMountain, OldLadyRanch, Campsite }, 20f, 10f);
        // PathB = sendero de MIEDO (este, peligro): Tumba → Rehenes → Delincuentes.
        public static readonly Vector2[] PathB      = Snake(new[] { Grave, HostageArea, MainCriminalCamp }, 20f, 10f);
        public static readonly Vector2[] GraveToCriminals     = Snake(new[] { Grave, new Vector2(849, 437), MainCriminalCamp }, 18f, 8f);
        public static readonly Vector2[] CriminalsToSecondary = Snake(new[] { MainCriminalCamp, new Vector2(1010, 518), SecondaryCamp }, 18f, 8f);

        // River runs along the east edge, next to the campsite. Smooth wavy
        // Catmull-Rom curve (same technique as the paved route) instead of a few
        // straight segments, with pronounced S-bends. Swings WEST toward the
        // campsite at z=335 so the fishing beach sits right beside camp.
        // Río del PLANO: vertical al centro (~x595), separa OESTE (humano) de ESTE
        // (peligro). Hace una curva al oeste en z~361 para pasar por la playa de pesca.
        static readonly Vector2[] RiverControls = {
            new Vector2(605, -60),
            new Vector2(600, 180),
            new Vector2(575, 300),
            new Vector2(545, 361),   // curva a la playa de pesca (orilla oeste)
            new Vector2(575, 450),
            new Vector2(600, 600),
            new Vector2(602, 760),
            new Vector2(596, 905),
            new Vector2(600, 1080)
        };
        public static readonly Vector2[] River = BuildSmoothRoute(RiverControls, 18f);

        // Segundo río (tributario): baja del lago "Montaña y Lago" (oeste) y desemboca
        // en el río principal. Del plano del owner (rio2).
        static readonly Vector2[] River2Controls = {
            new Vector2(175, 640),   // sale del lago
            new Vector2(300, 555),
            new Vector2(453, 479),
            new Vector2(554, 356),
            new Vector2(600, 285)    // desemboca en el río principal
        };
        public static readonly Vector2[] River2 = BuildSmoothRoute(River2Controls, 18f);

        // Mini playa de pesca + sendero desde el campamento hasta el agua (pedido del
        // owner: caminito corto del campamento a una playita donde se pueda pescar).
        public static readonly Vector2 RiverBeach   = new Vector2(524, 361);
        public static readonly Vector2[] BeachPath  = { Campsite, RiverBeach };

        // Caminos nuevos del owner (editor de plano) — senderos a pie que conectan la
        // zona central "MONTAÑA Y LAGO" con el resto, más el desvío a "MUERTE CAMINO
        // EQUIVOCADO". Se tratan como senderos (limpian árboles + pasto corto), como PathA.
        public static readonly Vector2[] Camino9  = { HuntingField, new Vector2(290, 430), Campsite };            // c10: campo de caza → campamento
        public static readonly Vector2[] Camino10 = { LakeMountain, new Vector2(200, 520), HuntingField };        // c16: lago → campo de caza
        public static readonly Vector2[] Camino11 = { OldLadyRanch, new Vector2(312, 560), LakeLookout };         // tMirW: vieja → mirador oeste
        public static readonly Vector2[] Camino12 = { LakeLookout, new Vector2(403, 449), Campsite };             // c20: mirador oeste → campamento
        public static readonly Vector2[] Camino13 = { WrongTurnDeath, new Vector2(247, 187), DirtTurnoff };       // c13: muerte camino equivocado → desvío
        // Cruce del río + red del ESTE (peligro):
        public static readonly Vector2[] Camino14 = { OldLadyRanch, new Vector2(560, 600), LookoutEast };         // c12: vieja → (cruza el río) → mirador este
        public static readonly Vector2[] Camino15 = { Campsite, new Vector2(534, 542), LookoutEast };             // c21: campamento → mirador este
        public static readonly Vector2[] Camino16 = { LookoutEast, new Vector2(775, 625), Grave };                // mt: mirador este → tumba
        public static readonly Vector2[] Camino17 = { SecondaryCamp, new Vector2(881, 619), Grave };              // st: secundario → tumba
        public static readonly Vector2[] Camino18 = { SecondaryCamp, new Vector2(780, 473), LookoutEast };        // tMirE: secundario → mirador este
        public static readonly Vector2[] Camino19 = { MainCriminalCamp, new Vector2(919, 453), HostageArea };     // cr: delincuentes → rehenes
        public static readonly Vector2[] Camino20 = { Grave, CabinEast, EscapePoint };                            // c15: tumba → cabaña este → escape
        public static readonly Vector2[] Camino21 = { MainCriminalCamp, new Vector2(808, 268), EscapePoint };     // c16e: delincuentes → escape
        // Todos ondulados en S (no líneas rectas).
        public static readonly Vector2[][] ExtraTrails = {
            Snake(Camino9, 16f, 8f), Snake(Camino10, 16f, 8f), Snake(Camino11, 16f, 8f),
            Snake(Camino12, 16f, 8f), Snake(Camino13, 16f, 8f), Snake(Camino14, 18f, 8f),
            Snake(Camino15, 16f, 8f), Snake(Camino16, 16f, 8f), Snake(Camino17, 16f, 8f),
            Snake(Camino18, 16f, 8f), Snake(Camino19, 14f, 8f), Snake(Camino20, 16f, 8f),
            Snake(Camino21, 16f, 8f)
        };

        // ===== ZONA CENTRAL: montañas + lago gigante (owner: unir Campo de Caza +
        // Montaña y Lago en una gran cuenca de montañas con un lago enorme, sin camino
        // entre ellas — se cruza el terreno natural). Todo tuneable acá.
        public static readonly Vector2 CentralLakeCenter = new Vector2(135, 645); // = "Montaña y Lago" (oeste lejano)
        public const float CentralLakeRadius = 65f;   // lago (radio pedido por el owner)
        public const float CentralLakeLevel  = 11f;   // altura del plano de agua
        public const float CentralLakeBed    = 3f;    // fondo carvado (bajo el agua)
        public const float CentralLakeShore  = 45f;   // ancho de la orilla que sube del fondo al terreno
        // Picos de montaña que rodean el lago (deben estar FUERA del radio del lago).
        public static readonly Vector2[] CentralPeaks = { new Vector2(60, 730), new Vector2(235, 700), new Vector2(120, 545) };
        public const float CentralPeakHeight = 92f;   // picos ALTOS con nieve (antes 46)
        public const float CentralPeakSigma  = 44f;   // más puntiagudos (antes 56)
        public const float SnowLine          = 82f;   // altura donde empieza la nieve en los picos

        // Paths that must feel scary: narrow + dense dry forest tunnel on top
        public static readonly Vector2[][] ScaryPaths = { PathB, CriminalsToSecondary, GraveToCriminals };

        // The paved route is a function of x (its waypoints strictly increase in x),
        // so we can ask "what z is the road at this x?" and thereby tell which side of
        // the road a point is on. The LAKE side is everything SOUTH of the road
        // (z < road z) - the side away from the forest/park (as in the RN40 photos:
        // guardrail, then the lake, then mountains on the far shore). North = forest.
        public static float PavedRouteZAt(float x)
        {
            var r = PavedRoute;
            if (x <= r[0].x) return r[0].y;
            for (int i = 0; i < r.Length - 1; i++)
            {
                if (x <= r[i + 1].x)
                {
                    float t = (x - r[i].x) / (r[i + 1].x - r[i].x);
                    return Mathf.Lerp(r[i].y, r[i + 1].y, t);
                }
            }
            return r[r.Length - 1].y;
        }

        // Samples a Catmull-Rom spline through the control points into a fine polyline
        // (~`spacing` metres between points), so the road reads as a genuine smooth
        // curve rather than straight segments with visible kinks.
        static Vector2[] BuildSmoothRoute(Vector2[] ctrl, float spacing)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < ctrl.Length - 1; i++)
            {
                Vector2 p0 = ctrl[Mathf.Max(0, i - 1)];
                Vector2 p1 = ctrl[i];
                Vector2 p2 = ctrl[i + 1];
                Vector2 p3 = ctrl[Mathf.Min(ctrl.Length - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / spacing));
                for (int s = 0; s < steps; s++)
                    pts.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
            }
            pts.Add(ctrl[ctrl.Length - 1]);
            return pts.ToArray();
        }

        // Ondula una polilínea: inserta un punto medio por segmento desplazado
        // PERPENDICULAR (alternando lado) para que no sea recta, y luego la suaviza con
        // Catmull-Rom → curvas en "S". `amp` = cuánto se desvía (m); `spacing` = fineza.
        // Los extremos quedan fijos (siguen pegados a las zonas).
        static Vector2[] Snake(Vector2[] pts, float amp, float spacing)
        {
            if (pts.Length < 2) return pts;
            var wp = new List<Vector2>();
            int sign = 1;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                wp.Add(pts[i]);
                Vector2 a = pts[i], b = pts[i + 1];
                Vector2 seg = b - a;
                if (seg.sqrMagnitude < 1f) continue;
                Vector2 perp = new Vector2(-seg.y, seg.x).normalized;
                wp.Add((a + b) * 0.5f + perp * amp * sign);
                sign = -sign;
            }
            wp.Add(pts[pts.Length - 1]);
            return BuildSmoothRoute(wp.ToArray(), spacing);
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ------------- Tunnel entrance (east end, where the game begins) -------------
        // The player spawns inside the tunnel facing west and drives out into the night.
        // Portal face is at TunnelEntranceX, facing EAST (+X). The tube and cliff
        // extend WEST (negative X) from the portal. The player spawns inside the
        // tube and drives east — out of the tunnel and onto the open night road.
        public const float TunnelEntranceX   =  30f;  // x of the portal face (east side of cliff)
        public const float TunnelHalfWidth   =  5.5f; // ±Z from road centre (matches road mesh)
        public const float TunnelRectHeight  =  4.5f; // height of the rectangular portion before the arch starts
        public const float TunnelLength      = 55f;   // how far the tube extends west (player spawns 30m in)
        public const float TunnelPortalDepth =  3.0f; // thickness of the stone arch frame
        public const float TunnelFrameWidth  =  3.5f; // width of stone framing around the arch opening

        // Manual fine-placement of the whole tunnel group, captured from the scene
        // after the owner nudged it by hand (Inspector → Tunnel → Transform).
        // Applied to the "Tunnel" group at the end of TunnelBuilder.Build so the
        // hand-tuned position survives a full map regenerate. If you move the
        // tunnel again in-editor, read the new Tunnel Transform and update these.
        public static readonly Vector3 TunnelGroupOffset = new Vector3(0.2f, -15.9f, -77.7f);
        public const float TunnelGroupYaw = 2.777f;   // degrees around Y (from quat y=0.02423, w=0.99971)
        public static readonly Vector3 TunnelGroupScale = new Vector3(1.7035f, 1.9401107f, 2.1910574f);

        // ------------- Lakeside (south of the paved route) -------------
        // The road sits on a low embankment; behind the guardrail the ground drops to
        // a lake that runs off past the map edge, with the skybox mountain silhouettes
        // (see EnvironmentBuilder.BuildDuskSky) reading as the far shore.
        public const float RoadSurfaceHeight = 17f;  // must match TerrainBuilder's paved-route flatten target
        public const float LakeLevel         = 13f;  // water plane height (~4m below the road)
        public const float LakeBedHeight     = 7f;   // carved lakebed floor (well below the waterline)
        public const float LakeShoulderWidth = 10f;  // south distance from road centre where the embankment starts dropping
        public const float LakeSlopeWidth    = 26f;  // metres over which it drops from shoulder height to lakebed
        public const float GuardrailOffset   = 5.5f; // guardrail distance south of the road centreline
        public const float GuardrailPostStep = 6f;   // spacing between guardrail posts

        // Shore vegetation band: the strip between the guardrail and the waterline
        // gets grass, a few bushes and some small young pines (owner request), rather
        // than being bare gravel. ShoreVegNear/Far are south-distances from the road
        // centre; the true waterline sits a little past ShoreVegFar so the very edge
        // stays a bare wet margin.
        public const float ShoreVegNear    = 6f;    // just behind the guardrail
        public const float ShoreVegFar     = 16f;   // stop just short of the water
        public const float ShorePineDensity = 0.06f; // sparse small pines
        public const float ShoreBushDensity = 0.13f; // some bushes

        // ------------- Ground tuning -------------
        // Cuánto barro (capa Muddy) se mezcla en el suelo base de TODO el mapa
        // (pedido del owner: el piso tiene que ser TIERRA, no verde).
        // 0 = pasto verde puro, 1 = barro puro. Manchones Perlin grandes le
        // suman/restan ~±0.2 para que no quede un color plano.
        public const float BaseMudBlend = 1.0f;   // todo el suelo base = barro (owner: "todo barro, más marrón")

        // Degradado de densidad del pasto por distancia a "zonas jugables" (caminos,
        // senderos y POIs). El jugador casi siempre anda cerca de esos lugares, así
        // que el pasto del bosque profundo lejos de todo casi nunca se ve de cerca
        // (y el pasto cercano lo tapa) → se puede ralear para ganar FPS sin que se
        // note. Denso dentro de GrassFullRadius, baja hasta GrassFarDensity a los
        // GrassFarRadius. Bajá GrassFarDensity para bosque profundo más pelado.
        // Fade por distancia del césped (shader Folklore/GrassFade): en vez de que
        // el césped aparezca de golpe al cruzar el corte de render, se desvanece con
        // dither en los últimos GrassFadeMargin metros. No cuesta FPS (mismo corte).
        // Poné GrassDistanceFade=false para volver al material del pack (sin fade).
        public const bool  GrassDistanceFade = true;   // re-activado con color más oscuro (tipo hojas de árbol) y corte más lejos
        public const float GrassFadeMargin   = 4f;   // metros antes del corte donde empieza a desvanecerse
        // Multiplicador de color del césped SOLO de día (el sol lo aclara). Verde >
        // rojo + azul bajo + brillo bajo = verde oscuro/quemado. Blanco = sin cambio.
        public static readonly Color GrassDayTint = new Color(0.34f, 0.42f, 0.20f);

        public const float GrassFullRadius = 6f;    // pasto pleno dentro de este radio de un camino/POI
        public const float GrassFarRadius  = 20f;   // a esta distancia ya llegó al piso ralo (degradado corto = mucho terreno ralo)
        public const float GrassFarDensity = 0.14f; // fracción de pasto en el bosque profundo (0 = pelado)

        // La textura "Muddy" del pack es un marrón-oliva verdoso, así que aunque se
        // pinte casi todo barro seguía leyéndose verde. TerrainBuilder tiñe su
        // diffuse por este color (multiplica) para forzar un tierra/barro cálido
        // inequívoco. Subí R / bajá G para más marrón; subí todo para aclarar.
        public static readonly Color MudTint = new Color(0.52f, 0.36f, 0.22f);  // marrón barro más profundo/cálido

        // ------------- Forest tuning -------------
        // Pushed back up now that AlanTree (light, ~a few k tris) replaced the old
        // 88k-tri ForestPack tree - the earlier 134M-tris/42FPS spike happened with
        // that much heavier mesh at this same kind of density, so this should land
        // in a very different (much better) place now. Re-check Stats after
        // regenerating and dial up/down from here.
        // Eased back down a notch (owner: "baja un poco la densidad") - was so dense
        // it read as a wall of identical trunks; the wider per-instance size range
        // below (see ScatterTrees) plus a bit more breathing room here should make
        // individual trees actually distinguishable from their neighbors.
        // Density eased back for the detailed BOTD conifers (2.6 + 0.85 densities was
        // 32 FPS). Still a dense pine forest, just fewer full-mesh trees to draw.
        // AHORA el bosque son pinos PSX de 24-30 triángulos (no los BOTD de 88k), así
        // que la densidad ya no cuesta casi nada → subo slots y densidades (owner:
        // "necesito que puebles más de árboles"). ~2x los árboles de antes.
        public const float TreeGridStep         = 2.2f;   // meters between candidate tree slots (tighter = more trees)
        public const float ScaryPathTreeDensity = 0.92f;  // closed dark tunnel (Path B & criminal territory)
        public const float PathATreeDensity     = 0.82f;  // green tunnel - also covers right up to path edges now
        public const float ForestTreeDensity    = 0.90f;  // owner: "más árboles" - bosque más denso
        public const float FieldTreeDensity     = 0.32f;  // isolated dry trees in the hunting field

        // AlanTree.fbx (Assets/ExternalAssets/ALanTree) replaces the old ForestPack
        // tree - a normal, lightweight single-tree asset instead of an 88k-tri hero
        // asset. BuildALanTreePrototypes() normalizes it to this height regardless
        // of its native fbx scale (the old tree's un-normalized 31m native height
        // is what caused the "giant tree blocking the path" bug).
        public const float RealTreeTargetHeight = 8.5f;

        // Yughues Free Bushes (Assets/YughuesFreeBushes2018) - scattered as their own
        // pass in ForestBuilder, independent of the tree density/mix above (bushes
        // are undergrowth, not canopy, so they get their own grid/density).
        public const float BushTargetHeight = 1.9f;
        public const float BushGridStep     = 4f;
        public const float BushDensity      = 0.35f;

        // Ground clutter (fallen logs + rocks, ForestBuilder.ScatterClutter) - the
        // messy natural forest floor. Its own sparse grid so it doesn't overwhelm.
        public const float ClutterGridStep = 14f;
        public const float ClutterDensity  = 0.55f;

        // Mud puddles (dark glossy water quads, ForestBuilder.ScatterPuddles) - wet
        // muddy road + a few in low forest spots.
        public const float PuddleRoadChance   = 0.5f;  // chance of a puddle at each ~3m road step
        public const float PuddleGridStep     = 28f;   // spacing of forest puddle candidates
        public const float PuddleForestChance = 0.25f; // chance a forest candidate becomes a puddle

        // Owner doesn't want the round procedural trees visible at all anymore -
        // 100% AlanTree. Procedural GreenTree/DryTree prefabs stay in the code as
        // the automatic fallback for if AlanTree.fbx is ever missing (see
        // ForestBuilder.Build), they just never get picked while this is 1.
        public const float RealTreeMixFraction  = 1f;

        // ------------- Night tuning -------------
        // Owner wants this genuinely pitch-black outside of direct light (flashlight,
        // campfire, torches) - not just "dark with visible moonlit silhouettes". Moon
        // is now barely more than a rim-light hint, ambient is near-zero.
        // Fog was cranked to 0.24 trying to make it "darker", but at that density the
        // torch beam couldn't punch through it - trees a few meters away were fogged
        // to black even when lit, which is a big part of why "no trees appear". The
        // darkness should come from black ambient + no moon (below), NOT from choking
        // fog. Lower fog lets the torch actually reveal the forest; everything
        // outside the beam is still pitch black.
        public const float FogDensity    = 0.05f;   // deep blue night murk
        public const float DayFogDensity          = 0.015f; // solo se usa si se vuelve a ExponentialSquared
        // VISTA ABIERTA DE DÍA (owner: hay FPS de sobra, estirar la distancia de día).
        // De día es golden-hour abierto: la niebla arranca más lejos y termina lejos,
        // los árboles llegan a ~115m (billboards baratos) y la cámara a 150m. El pasto
        // se estira a 52m — el shader de fade tapa el corte, no depende de la niebla.
        public const float DayFogStart            = 30f;   // niebla lineal: comienza a 30m
        // VISTA ABIERTA de día (hay MUCHO FPS de sobra): se ve el bosque de la orilla
        // de enfrente. Niebla lejana + árboles a 130m (billboards baratos). El pasto
        // acompaña a 55m para que no quede banda pelada en tierra.
        public const float DayFogEnd              = 90f;   // bajado un poco (el FPS alto era mirando al vacío, no al bosque)
        public const float DayDetailRenderDistance = 50f;  // pasto a 50m (acompaña; el fade + fog parcial disimulan el borde)
        public const float DayTreeRenderDistance   = 105f; // bosque de la orilla lejana (billboards baratos)
        public const float DayCameraFarClip        = 118f; // terreno/árboles lejanos; montañas = skybox
        public static readonly Color DayFogColor = new Color(0.68f, 0.54f, 0.58f); // neblina cálida violácea — FtF golden-hour haze
        public const float MoonIntensity = 0.16f; // dim deep-blue moon - dark FtF night, silhouettes still read

        // Spot light (see TestPlayerBuilder.cs). Wider cone + longer reach so a much
        // bigger area of ground is lit.
        public const float FlashlightRange = 34f;
        public const float FlashlightSpotAngle = 75f;

        // ------------- Performance / render distances -------------
        // Keyed off the torch's reach: trees/grass beyond the beam are invisible in
        // the darkness anyway. Billboard distance is set EQUAL to render distance on
        // purpose (see ForestBuilder) so terrain trees always draw as their real mesh
        // and never switch to the broken billboard LOD (this asset has no billboard
        // shader - that's the "Nature/Soft Occlusion" warning).
        // With fog this thick, anything past ~25m is already black - so render
        // distances are pulled in tight, which is also what lets us afford the much
        // higher tree density (fewer visible at once even though far more exist).
        // Navigable night, but trees only render to ~50m (rendering thousands of
        // real-mesh trees to 90m was the 30-FPS killer). The light mist fades them
        // out around there anyway, so the forest still reads with depth.
        // Now 100% BOTD conifers (which HAVE billboards), so billboarding is ON:
        // trees render as full mesh up close and switch to cheap billboards past
        // BillboardDistance. This lets us render trees much FARTHER (deep forest into
        // the fog) for far LESS cost than the old all-mesh setup.
        // OPTIMIZACIÓN (owner: más FPS): de noche la niebla exp² (0.05) vuelve todo
        // negro pasando los ~40m, así que dibujar árboles hasta 80m y terreno hasta
        // 220m era puro gasto invisible. Cortados a 48m (árboles) y 100m (cámara):
        // sigue habiendo margen sobre el punto donde la niebla ya lo ocultó todo.
        public const float TreeRenderDistance    = 55f;   // punto medio: bosque de fondo tupido pero menos árboles que dibujar que a 70m (más FPS mínimo)
        // Opción 1 (casi invisible): cuántos árboles como MALLA COMPLETA a la vez.
        // El resto pasa a billboard barato. Bajarlo descarga el dibujado sin que se
        // note (los de lejos ya eran billboards). Default de Unity = 50.
        public const int   TreeMaxFullLOD        = 14;  // bajado de 20 (owner: optimizar árboles sin romperlos): menos árboles a malla 3D a la vez, el resto billboard barato. El crossfade lo suaviza. REVERTIR: poné 20.

        // Translucidez de las hojas (luz atravesando las agujas). Cara por pixel; de
        // día da el glow a contraluz, de noche no se ve. false = apagada (más FPS,
        // árboles más planos de día). REVERTIR: poné true y regenerá. NO toca el
        // viento de las hojas (eso queda intacto).
        public const bool  TreeLeafTranslucency = true;   // REVERTIDO: apagarla no dio FPS y sacaba el glow de día → se deja como estaba
        public const float TreeCrossFade         = 20f;   // metros de transición suave malla->billboard (para que no "salte")
        public const float TreeBillboardDistance = 10f;    // full mesh only within this; cheap billboards beyond -> big FPS win
        public const float DetailRenderDistance  = 15f;   // subido (owner: "no tan bajo"); el fade del césped termina acá; hay FPS de sobra
        public const float DetailDensity         = 0.28f; // fraction of detail objects actually drawn (0-1)
        public const float TerrainBasemapDistance = 70f;  // ground texture at full res only up close
        public const float CameraFarClip         = 85f;   // por encima de la distancia de árboles (70m) para no cortar el bosque de fondo; montañas lejanas = skybox
        public const float ShadowDistance        = 20f;   // realtime shadow render range (URP asset)
    }
}
