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
        public const float MapSize  = 413f;        // z extent (metres) — 2ª pasada de reducción, factor 0.75 (owner: "achicalo más")
        public const float MapSizeX = 600f;        // x extent (metres) — 2ª pasada de reducción, factor 0.75
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

        // Vallas de madera: 2 intentos de auto-colocarlas a lo largo del camino
        // (escala/rotación) no dieron resultado (owner: "ahora ni siquiera estan
        // mejor decime como ponerlas yo") -- APAGADO, el owner las va a poner a
        // mano en el Editor. FenceBuilder.cs queda intacto por si se retoma más
        // adelante (el material ya generado, Assets/Settings/WoodenFence.mat,
        // sirve igual para colocarlas a mano — ver instrucciones del owner).
        public const bool BuildFences = false;
        public const float FenceOffsetDirtRoad = 5f;   // separación del camino de auto (más ancho)
        public const float FenceOffsetCamino10 = 3.5f; // separación del sendero a pie (más angosto)

        // ------------- Key locations (x, z) — matches the hand-drawn plan -------------
        // (DirtTurnoff is derived from the smooth paved route further down, so it always
        //  sits exactly on the road wherever the road is at x=620.)
        // Coordenadas del PLANO FINAL del owner (MapPlan.html). Layout de dos lados:
        // OESTE = humano, ESTE = peligro, río al medio (~x595).
        public static readonly Vector2 Campsite         = new Vector2(246f, 232f);  // campamento junto al río — corrido un poco más al oeste (258->246, owner: "muevelo un poquito a la izquierda") para darle más lugar al descenso hacia el agua sin perder el nucleo plano (dc<12, ver TerrainBuilder)
        // INTERCAMBIADOS (owner, editor de plano): VIEJA <-> CAMPO DE CAZA. Posición EXACTA
        // de VIEJA ajustada a mano por el owner en el editor de plano (235,388). Todo el
        // combo de la vieja (casa/galpón/huellas/lote/BarnPath) se recalcula desde ese
        // ancla con los MISMOS offsets relativos originales, para no perder su forma.
        // CAMINOS MAS CORTOS (owner: "necesito mas cortos los caminos, menos los que
        // desembocan hacia la ruta y debo ir en auto, esos estan bien"). Todos los
        // destinos que se caminan (NO Campsite/DirtRoad/PavedRoute/DirtTurnoff, esos
        // se manejan en auto) se acercaron ~30% a su hub real:
        //  - Lado oeste/humano -> hacia Campsite (el hub real de esos senderos).
        //  - Mirador Oeste -> hacia LakeMountain (cuelga del lago, no del campamento).
        //  - Lado este/peligro -> hacia LookoutEast (la "puerta" del cruce del río,
        //    que se deja FIJA a proposito para no mover el puente/cruce).
        //  - LakeMountain/CentralLakeCenter: owner pidió "un lago mucho más chico...
        //    con árboles alrededor" (referencia: la laguna en el bosque de Fears to
        //    Fathom - Ironbark Lookout) — se ACHICÓ (ver más abajo, sección LAGO).
        //    POSICIÓN: 2 intentos previos (cerca del campamento en 220,150 -- quedó
        //    encima del DirtRoad; de vuelta al original 71,293 -- lejos de nuevo) hasta
        //    que el owner, mirando el bosque quemado ya poblado de árboles en la
        //    escena, pidió "el lago deberia estar ahi donde esta el bosque quemado" --
        //    ahora ocupa las coordenadas viejas de BurntForest (176,257), que ya tenía
        //    bosque denso alrededor y ningún camino cerca. Las montañas que colgaban
        //    del mismo punto se desacoplaron (CentralPeakHeight=0): una laguna chica
        //    de bosque no necesita una cordillera nevada detrás.
        // Todos los midpoints de Camino9/10/11/13 y del lado este se recalcularon con
        // la MISMA transformacion (mismo pivote+factor que sus extremos), para que la
        // forma/proporcion de cada sendero quede igual, solo mas chico — no un punto
        // medio viejo que ya no corresponde (la razon real del bug de "caminos
        // enredados" de hace unas rondas).
        public static readonly Vector2 OldLadyRanch     = new Vector2(182f, 178f);  // "VIEJA" (waypoint del camino) — acercada al campamento (30%)
        // centro real de la CASA de la vieja: corrida al costado del camino (Camino10 pasa
        // por OldLadyRanch, rama directa desde el campamento). Lo usan HouseBuilder (dónde
        // colocar la casa) y ForestBuilder (dónde despejar el pasto bajo la huella).
        // Se TRASLADA (no escala) junto con OldLadyRanch para no achicar la casa en sí.
        public static readonly Vector2 OldLadyHouseCenter = new Vector2(185f, 178f);
        // galpón/granero de la vieja: a la IZQUIERDA y un poco atrás de la casa (oeste
        // + algo al norte), cerca. Lo usan HouseBuilder (colocar) y ForestBuilder
        // (despejar pasto/árboles bajo él).
        public static readonly Vector2 OldLadyBarnCenter = new Vector2(178f, 182f);

        // HUELLAS rectangulares (min/max en x,z world) de la casa y el galpón, para que
        // ForestBuilder despeje el pasto EXACTO bajo cada edificio (el despeje por radio
        // dejaba pasto atravesando el piso en las esquinas). Ajustar si sobra/falta.
        // (mapa reducido 0.7: el CENTRO se movió con el resto, pero el TAMAÑO de la huella
        //  se mantiene porque el edificio NO se achicó.)
        public static readonly Vector2 OldLadyHouseFootMin = new Vector2(176f, 169f);
        public static readonly Vector2 OldLadyHouseFootMax = new Vector2(197f, 187f);
        public static readonly Vector2 OldLadyBarnFootMin  = new Vector2(173f, 178f);
        public static readonly Vector2 OldLadyBarnFootMax  = new Vector2(181f, 188f);
        public static bool InRect(Vector2 p, Vector2 mn, Vector2 mx, float m) =>
            p.x > mn.x - m && p.x < mx.x + m && p.y > mn.y - m && p.y < mx.y + m;
        public static readonly Vector2 HuntingField     = new Vector2(220f, 300f);  // "CAMPO DE CAZA" (oeste) — acercado al campamento (30%)
        public static readonly Vector2 Grave            = new Vector2(395f, 211f);  // "TUMBA" — este, acercada al Mirador Este (30%)
        public static readonly Vector2 MainCriminalCamp = new Vector2(443f, 182f);  // "DELINCUENTES PRINCIPAL" (este) — acercado al Mirador Este (30%)
        public static readonly Vector2 HostageArea      = new Vector2(448f, 218f);  // "REHENES" (este) — acercada al Mirador Este (30%)
        // Laguna chica de bosque (owner: "un lago mucho mas chico... con arboles todo
        // al rededor", ref. la laguna de Fears to Fathom - Ironbark Lookout). Primer
        // intento (220,150) quedó a 10m de un waypoint del DirtRoad -- literalmente
        // encima de la ruta de auto al campamento (owner: "me lo pusiste de frente en
        // medio del camino"). Segundo intento: volver a la posición original
        // (71,293) -- funcionaba pero seguía lejos del campamento, que era el pedido
        // original. Definitivo (owner, mirando el bosque quemado en la escena: "el
        // lago deberia estar ahi donde esta el bosque quemado") -- la laguna ocupa
        // ahora las coordenadas viejas de BurntForest (176,257), que ya tenía bosque
        // denso alrededor (por eso se veía bien en la captura) y no tiene ningún
        // camino/ruta cerca. BurntForest se reubicó unos metros al oeste (ver más
        // abajo) para no superponerse.
        // Corrida un poquito más lejos del campamento/camino (owner: "mové el lago un
        // poquititito mas adelante asi entra la casa del lado del camino" -- va a
        // poner una casa a mano cerca del camino, necesita ese margen). Empuje chico
        // (~8m) en la dirección OPUESTA a la que viene el camino (Campsite->Lago),
        // así se abre lugar de ese lado sin mover el lago demasiado.
        public static readonly Vector2 LakeMountain     = new Vector2(168.5f, 259.7f);  // "LAGUNA" — antes bosque quemado; ver BurntForest para su reubicación
        public static readonly Vector2 WrongTurnDeath   = new Vector2(142f, 151f);  // "MUERTE CAMINO EQUIVOCADO" (oeste, spur) — acercada al campamento (30%)
        // Reubicado junto con la laguna (offset ~24m desde el centro, hacia el
        // campamento, mismo criterio que las veces anteriores). Trasladado el mismo
        // delta que LakeMountain (-7.5,+2.7) cuando el lago se corrió "un poquititito
        // mas adelante" -- si no, quedaba mal calibrado respecto al lago nuevo.
        public static readonly Vector2 LakeLookout      = new Vector2(191.5f, 251.7f);  // "MIRADOR" — cuelga de la laguna
        public static readonly Vector2 AbandonedCabin   = new Vector2(263f, 147f);  // "CABAÑA OESTE" — acercada al campamento (30%)
        // Zonas NUEVAS del plano de dos lados:
        public static readonly Vector2 EscapePoint      = new Vector2(406f, 106f);   // "ESCAPE" — acercado al Mirador Este (30%)
        public static readonly Vector2 CabinEast        = new Vector2(361f, 156f);  // "CABAÑA ESTE" — acercada al Mirador Este (30%)
        public static readonly Vector2 LookoutEast      = new Vector2(367f, 260f);  // "MIRADOR ESTE" — orilla este del río. SIN TOCAR: es la "puerta" fija del cruce, todo el lado este se acerca a ESTE punto

        // ------------- Lote de la casa de la vieja (HouseBuilder) -------------
        // Bounds en world XZ del cerco (deben coincidir con la valla de HouseBuilder:
        // grupo en OldLadyRanch-(8,7) [casa en L, bounding 16×14], valla local
        // x[-6..30] z[-7..19] → world abajo). TerrainBuilder aplana este rectángulo a
        // OldLadyLotHeight y ForestBuilder no pone árboles adentro (+ un margen).
        // Ajustar si cambia la valla o el centrado de la casa.
        public static readonly Vector2 OldLadyLotMin = new Vector2(166f, 164f);  // trasladado junto con OldLadyRanch (mismo delta, sin achicar el lote)
        public static readonly Vector2 OldLadyLotMax = new Vector2(202f, 190f);  // trasladado junto con OldLadyRanch (mismo delta, sin achicar el lote)
        public const float OldLadyLotHeight = 25.5f;   // nivel plano del lote (≈ altura natural ahí)

        // Claro sin pasto alrededor del campamento del jugador (fogata + troncos +
        // carpas + mesa). El dressing de CampsiteBuilder llega ~7-8m del centro y el
        // pasto es de 4-7m de alto (billboards), así que el rooteado justo en el borde
        // "se asoma" sobre la fogata → 11m lo empuja bien lejos y deja el suelo pelado.
        public const float CampsiteClearRadius = 11f;

        // ------------- Paths (polylines, x/z) -------------
        // The paved route is a smooth Catmull-Rom curve through a handful of gentle,
        // widely-spaced control points (NOT the old up/down/up/down zig-zag). Long
        // wavelength + small amplitude = a believable rural road with sweeping bends.
        // Control points run past both map edges (x < 0 and x > MapSize) so the road
        // enters and leaves the terrain mid-curve instead of ending square-on.
        static readonly Vector2[] PavedControls = {
            new Vector2(-136f, 45f),
            new Vector2(79f, 37f),
            new Vector2(273f, 48f),
            new Vector2(462f, 38f),
            new Vector2(620f, 47f),
            new Vector2(788f, 39f),   // long approach across the extended east half of the map
            new Vector2(872f, 43f)
        };
        // Sampled into a fine polyline (~22m spacing) so it reads as a true curve, not
        // a set of straights with kinks. Stays x-monotonic, so PavedRouteZAt still works.
        public static readonly Vector2[] PavedRoute = BuildSmoothRoute(PavedControls, 22f);

        // Where the dirt road leaves the paved route - kept exactly on the curve.
        public static readonly Vector2 DirtTurnoff = new Vector2(225f, PavedRouteZAt(225f)); // desvío corrido más al este/derecha sobre la ruta (owner)

        // ------------- ZONAS/POIs NUEVOS (ideas del MapPlan) — los construye AreaPoiBuilder -------------
        // Van DESPUÉS de DirtTurnoff/PavedRoute para que los de la ruta puedan usar PavedRouteZAt.
        public static readonly Vector2 EstepaCenter  = new Vector2(165f, 143f);  // estepa (campo abierto ventoso), sobre Camino13 — acercada junto con Camino13 (30%)
        public static readonly Vector2 Molino        = new Vector2(151f, 137f);  // molino de viento oxidado — acercado junto con la estepa (30%)
        public static readonly Vector2 Mallin        = new Vector2(252f, 323f);  // pantano (mallín), sobre Camino14
        // Roquedal (afloramiento de piedra) ELIMINADO (owner: "quita lo del roquedal
        // y pon arboles tambien") -- el punto en sí ya no se usa en ningún lado,
        // queda bosque normal ahí.
        // corrido ~46m al oeste (era 176,257) para dejarle el lugar a la laguna nueva
        // (owner: "el lago deberia estar ahi donde esta el bosque quemado").
        public static readonly Vector2 BurntForest   = new Vector2(130f, 285f);  // bosque quemado
        // orilla de la laguna + muelle — a ~14m del centro, del lado que mira al
        // campamento (misma lógica que las ubicaciones anteriores de la laguna).
        // Trasladada el mismo delta que LakeMountain (-7.5,+2.7) cuando el lago se
        // corrió "un poquititito mas adelante" -- si no, el muelle/rancho quedaban
        // calibrados contra la orilla VIEJA, no la nueva.
        public static readonly Vector2 LakeShore     = new Vector2(181.5f, 254.7f);
        public static readonly Vector2 HangedTree    = new Vector2(390f, 227f);  // árbol del ahorcado + cementerio (pegado a la Tumba) — trasladado junto con la Tumba
        public static readonly Vector2 Antenna       = new Vector2(370f, 282f);  // antena/repetidora (cerro)
        public static readonly Vector2 Corrales      = new Vector2(528f, 233f); // corrales/bañadero (junto a la estancia)
        public static readonly Vector2 Estancia      = new Vector2(512f, 211f);  // estancia + galpón (El Familiar)
        public static readonly Vector2 Capilla       = new Vector2(311f, 278f);  // capilla anegada (medio hundida en el río)
        // Sobre la RUTA (z derivado de la curva real del asfalto):
        public static readonly Vector2 DifuntaCorrea = new Vector2(310f, PavedRouteZAt(310f)); // santuario Difunta Correa
        public static readonly Vector2 GauchitoGil   = new Vector2(174f, PavedRouteZAt(174f)); // ermita Gauchito Gil (en el desvío)
        public static readonly Vector2 YpfStation    = new Vector2(449f, PavedRouteZAt(449f)); // estación YPF abandonada
        // PLATAFORMA de la YPF: lote plano al NORTE del asfalto (la "entrada" a la estación),
        // para que no quede sobre el borde alto de la ruta. Aplanado por TerrainBuilder,
        // sin árboles/pasto (ForestBuilder) y con piso de tierra (splat).
        public const float YpfPadHalfX = 14f;   // medio ancho del lote (x) desde YpfStation.x — ajustado a la estación (28m de ancho)
        // La ruta pintada tiene un HOMBRO de asfalto de ~12-14m al norte del centro (ver
        // TerrainBuilder.PaintTextures, Strip(dPavCentre,12f,14f) del lado norte) — el
        // lote tiene que arrancar apenas pasado eso para quedar PEGADO a la ruta sin pisarla.
        public const float YpfPadNearZ = 10f;   // borde CERCANO (sur) del lote — pegado a la ruta
        public const float YpfPadFarZ  = 34f;   // borde LEJANO (norte) del lote — lote más chico (24m de fondo)
        public static bool InYpfPad(Vector2 p)
        {
            float dz = p.y - PavedRouteZAt(p.x);
            return Mathf.Abs(p.x - YpfStation.x) < YpfPadHalfX && dz > YpfPadNearZ && dz < YpfPadFarZ;
        }
        // Caminos que salen del campamento: ahora en S (curvas suaves Catmull-Rom con
        // puntos que zigzaguean) en vez de líneas rectas (pedido del owner).
        // corrida más al este (owner: quedaba muy pegada a la Vieja) — mismo desvío/campamento, curva movida.
        // AMPLITUD/ESPACIADO de Snake() reescalados x0.525 (0.7*0.75, los mismos 2
        // achicados del mapa) — se habían quedado con los valores absolutos ORIGINALES
        // del mapa grande, por eso los senderos zigzagueaban el doble de lo debido y se
        // cruzaban entre sí (owner: "los caminos siguen igual/enredados").
        public static readonly Vector2[] DirtRoad   = Snake(new[] { DirtTurnoff, new Vector2(230f, 150f), Campsite }, 4f, 4f); // ruta de tierra en S
        // PathA = rama DIRECTA campamento ↔ lago (una de las 3 ramas del hub, owner).
        public static readonly Vector2[] PathA      = Snake(new[] { LakeMountain, Campsite }, 4f, 4f);
        // PathB = sendero de MIEDO (este, peligro): Tumba → Rehenes → Delincuentes.
        // Amplitud reescalada junto con el acercamiento del lado este (30%): 10->7, 5->3.5.
        public static readonly Vector2[] PathB      = Snake(new[] { Grave, HostageArea, MainCriminalCamp }, 7f, 3.5f);
        public static readonly Vector2[] GraveToCriminals     = Snake(new[] { Grave, new Vector2(422f, 197f), MainCriminalCamp }, 6.3f, 2.8f);

        // River runs along the east edge, next to the campsite. Smooth wavy
        // Catmull-Rom curve (same technique as the paved route) instead of a few
        // straight segments, with pronounced S-bends. Swings WEST toward the
        // campsite at z=335 so the fishing beach sits right beside camp.
        // Río del PLANO: vertical al centro (~x595), separa OESTE (humano) de ESTE
        // (peligro). Hace una curva al oeste en z~361 para pasar por la playa de pesca.
        static readonly Vector2[] RiverControls = {
            new Vector2(318f, -31f),
            new Vector2(315f, 95f),
            new Vector2(302f, 158f),
            new Vector2(287f, 190f),   // curva a la playa de pesca (orilla oeste)
            new Vector2(302f, 236f),
            new Vector2(315f, 315f),
            new Vector2(316f, 399f),
            new Vector2(313f, 476f),
            new Vector2(315f, 567f)
        };
        public static readonly Vector2[] River = BuildSmoothRoute(RiverControls, 18f);

        // Split OESTE (campo argentino) / ESTE (bosque de pino) para las especies de
        // árbol (ForestBuilder.ScatterTrees) — owner: "pongamos mitad y mitad, campo
        // argentino de un lado". El río corre en x~287-318 según RiverControls arriba;
        // 300 cae del lado oeste de esa franja, así el corte queda pegado al río real.
        public const float ForestSplitX = 300f;

        // Segundo río (tributario): baja del lago "Montaña y Lago" (oeste) y desemboca
        // en el río principal. Del plano del owner (rio2).
        static readonly Vector2[] River2Controls = {
            new Vector2(92f, 336f),   // sale del lago
            new Vector2(158f, 292f),
            new Vector2(238f, 251f),
            new Vector2(291f, 187f),
            new Vector2(315f, 150f)    // desemboca en el río principal
        };
        // QUITADO (owner): el tributario que cruzaba y llegaba al lago. Se deja en array
        // VACÍO → sin cauce (TerrainBuilder), sin agua (EnvironmentBuilder) y sin bloquear
        // árboles/pasto (DistToRivers). Para volver a activarlo: BuildSmoothRoute(River2Controls, 18f).
        public static readonly Vector2[] River2 = new Vector2[0];

        // Mini playa de pesca + sendero desde el campamento hasta el agua (pedido del
        // owner: caminito corto del campamento a una playita donde se pueda pescar).
        public static readonly Vector2 RiverBeach   = new Vector2(275f, 190f);
        public static readonly Vector2[] BeachPath  = { Campsite, RiverBeach };

        // Desde la playa de pesca al campo de caza (owner: "desde el camino de la pesca
        // se podra ir al campo de caza", reemplaza el viejo Camino9 directo
        // campamento→campo de caza). Midpoint corrido ~12m al oeste de la línea recta
        // (247,245) para que la curva no pase pegada al campamento (a 16m nomás en la
        // recta) y quede claramente un camino aparte, no un atajo disimulado.
        public static readonly Vector2[] BeachToHuntingField = { RiverBeach, new Vector2(235f, 248f), HuntingField };

        // Caminos nuevos del owner (editor de plano) — senderos a pie que conectan la
        // zona central "MONTAÑA Y LAGO" con el resto, más el desvío a "MUERTE CAMINO
        // EQUIVOCADO". Se tratan como senderos (limpian árboles + pasto corto), como PathA.
        // NOTA (owner intercambió VIEJA <-> CAMPO DE CAZA en el editor de plano): estos dos
        // caminos son LOCALES a la geografía (no al nombre), así que se re-conectan al que
        // ahora vive ahí. Camino10 pasó de apuntar a HuntingField (ahora lejos, cerca del
        // campamento) a apuntar a OldLadyRanch (ahora en ese barrio, junto al lago).
        // Camino14 pasó de OldLadyRanch (ahora lejos, en el oeste) a HuntingField (ahora
        // cerca del cruce del río, donde antes estaba la vieja).
        // NUDO DEL NOROESTE simplificado (owner: "muchos caminos entre el lago, el
        // campamento y el campo de caza" — debe haber, desde el campamento, UNA rama al
        // lago (PathA), UNA a la vieja (Camino9b, acá abajo) y UNA al campo de caza
        // (Camino9). El Mirador Oeste cuelga del LAGO, no del campamento (evita el 4to
        // camino directo al hub).
        // punto medio VIEJO (152,226) era de antes de mover Campo de Caza/Campamento —
        // hacía un rodeo de 103m hacia el oeste, atravesando toda la zona del lago/vieja
        // (la causa REAL del nudo, no la amplitud del zigzag). Recalculado cerca de la
        // línea directa entre los dos puntos actuales.
        // Camino9/10/11: midpoints recalculados con la MISMA transformación que sus
        // extremos (pivote Campsite x0.7 para 9/10, pivote LakeMountain x0.7 para 11)
        // para que la forma del sendero quede igual, solo más corto.
        // Camino9 (campamento → campo de caza, directo) ELIMINADO (owner: "el flujo es
        // campamento → lo de la vieja → a pescar, y desde el camino de la pesca se podrá
        // ir al campo de caza" — o sea, ya no hay atajo directo campamento-campo de caza,
        // hay que pasar por la playa de pesca). Reemplazado por BeachToHuntingField más
        // abajo (RiverBeach → HuntingField).
        public static readonly Vector2[] Camino10 = { Campsite, new Vector2(214f, 199f), OldLadyRanch };            // rama DIRECTA campamento → vieja (antes iba lago→vieja, redundante con PathA)
        // curva real (ya ondulada) de Camino10 -- la usa FenceBuilder para correr la
        // valla PEGADA al camino real, no a la línea recta entre los 3 puntos de control.
        public static readonly Vector2[] Camino10Path = Snake(Camino10, 4f, 4f);
        // midpoint recalculado otra vez para la posición nueva de la laguna.
        public static readonly Vector2[] Camino11 = { LakeMountain, new Vector2(175.5f, 257.7f), LakeLookout };          // laguna → mirador (antes vieja→mirador, ya no hace falta)
        // c13: muerte camino equivocado → owner: "que sea confuso", el desvío tiene que
        // aparecer a unos metros de entrar al camino de tierra (no 100+ metros adentro,
        // que era lo que quedaba pegado al punto medio de DirtRoad después de correr el
        // desvío/DirtTurnoff más al este). Salía a 15m, el owner pidió correrlo "unos
        // cuantos metros más adelante" → ahora a 38m después de DirtTurnoff. Midpoint
        // acercado junto con WrongTurnDeath (pivote Campsite, 30%); el otro extremo
        // (sobre DirtTurnoff/DirtRoad) NO se toca — es el auto, no un sendero a pie.
        public static readonly Vector2[] Camino13 = { WrongTurnDeath, new Vector2(165f, 138f), new Vector2(DirtTurnoff.x + 3f, DirtTurnoff.y + 38f) };
        // Cruce del río + red del ESTE (peligro). El punto medio de Camino14 (294,285)
        // NO se toca: es donde el río realmente se puede cruzar (posición fija del
        // terreno), no una distancia de hub que se pueda acortar.
        public static readonly Vector2[] Camino14 = { HuntingField, new Vector2(294f, 285f), LookoutEast };         // c12: campo de caza → (cruza el río) → mirador este (antes iba desde la vieja)
        // Camino15 (campamento → mirador este, directo) ELIMINADO (owner: "quita ese
        // camino del campamento hacia el puente, al que tiene que ir conectado es al
        // que esta en el campo de caza") -- el puente peatonal ahora se alinea con
        // Camino14 en vez de este camino (ver LandmarkBuilder).
        public static readonly Vector2[] Camino16 = { LookoutEast, new Vector2(395f, 266f), Grave };                // mt: mirador este → tumba
        public static readonly Vector2[] Camino19 = { MainCriminalCamp, new Vector2(448f, 203f), HostageArea };     // cr: delincuentes → rehenes
        public static readonly Vector2[] Camino20 = { Grave, CabinEast, EscapePoint };                            // c15: tumba → cabaña este → escape
        public static readonly Vector2[] Camino21 = { MainCriminalCamp, new Vector2(408f, 135f), EscapePoint };     // c16e: delincuentes → escape
        // caminito corto de la puerta de la casa al portón del galpón — trasladado
        // junto con OldLadyRanch (mismo delta, +27/+23) para no achicar la casa en sí.
        public static readonly Vector2[] BarnPath = {
            new Vector2(185f, 174f), new Vector2(182f, 177f), new Vector2(178f, 180f)
        };
        // Todos ondulados en S (no líneas rectas). Amplitud/espaciado reescalados junto
        // con el acercamiento del lado este (30%, Camino16/19/20/21): 8->5.6, 7->4.9.
        // Los del lado oeste (9/10/11/13) ya estaban chicos y no hacía falta tocarlos.
        public static readonly Vector2[][] ExtraTrails = {
            Snake(BeachToHuntingField, 4f, 4f), Camino10Path, Snake(Camino11, 4f, 4f),
            Snake(Camino13, 8f, 4f), Snake(Camino14, 9f, 4f),
            Snake(Camino16, 5.6f, 4f),
            Snake(Camino19, 4.9f, 4f), Snake(Camino20, 5.6f, 4f),
            Snake(Camino21, 5.6f, 4f), BarnPath
        };

        // ===== LAGUNA DE BOSQUE (owner: "quiero un lago mucho mas chico, como este
        // [ref. Fears to Fathom - Ironbark Lookout] con arboles todo al rededor" —
        // reemplaza el lago gigante lejano con montañas que había antes). Radio/playa
        // achicados ~3.5x (32->9, 45->10, 85->18). CENTRO: ver comentario largo más
        // arriba (junto a LakeMountain) — terminó en las coordenadas viejas de
        // BurntForest (176,257), pedido explícito del owner mirando la escena.
        // Profundidad (Level/Bed) sin tocar, ya estaba afinada.
        // Radio final (owner: "era demasiado grande dejalo en 23 o 23.5", después de
        // probar 9, 6 y 25) -- beachWidth/shore NO se tocaron, solo el radio del agua.
        public static readonly Vector2 CentralLakeCenter = LakeMountain; // = "LAGUNA" (antes bosque quemado)
        public const float CentralLakeRadius = 23.5f;
        public const float CentralLakeLevel  = 22f;   // altura del plano de agua (sin tocar, ya afinada)
        public const float CentralLakeBed    = 14f;   // fondo carvado (sin tocar, misma profundidad de agua)
        public const float CentralLakeBeachWidth = 6f; // franja plana de playa junto al agua
        public const float CentralLakeShore  = 10f;   // ancho TOTAL de orilla (playa + transición a terreno natural) — alcance total desde el centro: radio+shore
        // Forma OVALADA/rectangular en vez de círculo perfecto (owner, mostrando la
        // referencia de Fears to Fathom: "que sea un poco mas rectangular/ovalado el
        // lago no redondo"). El primer intento estiraba en X/Z del MUNDO -- pero el
        // camino (PathA) llega desde el campamento en un ángulo que casi coincide con
        // ese eje largo, así que el jugador veía la laguna "de punta" (mirando por el
        // eje largo hacia adentro) en vez de "de lado" (owner: "necesito que este de
        // lado mirando al camino... ahora esta de una punta mirando hacia el camino,
        // rota el lago"). Ahora el eje CORTO apunta hacia el campamento (la dirección
        // por la que se llega) y el eje LARGO queda perpendicular a esa línea, así al
        // caminar por PathA la laguna se ve de costado, ancha.
        public const float LakeStretchLong  = 1.55f; // eje ancho (perpendicular al camino)
        public const float LakeStretchShort = 0.82f; // eje angosto (mirando hacia el campamento)
        public static readonly float LakeAxisAngle =
            Mathf.Atan2(Campsite.y - LakeMountain.y, Campsite.x - LakeMountain.x) + Mathf.PI * 0.5f;
        // uso de EnvironmentBuilder, para rotar el plano de agua con el mismo ángulo
        // (yaw = -ángulo: Quaternion.Euler(0,yaw,0)*Vector3.right = (cos yaw, 0, -sin yaw)).
        public static readonly float LakeAxisYawDeg = -LakeAxisAngle * Mathf.Rad2Deg;
        static readonly Vector2 LakeLongAxis  = new Vector2(Mathf.Cos(LakeAxisAngle), Mathf.Sin(LakeAxisAngle));
        static readonly Vector2 LakeShortAxis = new Vector2(-LakeLongAxis.y, LakeLongAxis.x);
        public static float LakeDist(Vector2 p)
        {
            Vector2 rel = p - CentralLakeCenter;
            float u = Vector2.Dot(rel, LakeLongAxis)  / LakeStretchLong;
            float v = Vector2.Dot(rel, LakeShortAxis) / LakeStretchShort;
            float dist = Mathf.Sqrt(u * u + v * v);
            // Orilla ORGÁNICA (forma de "poroto", owner: referencia con una laguna
            // irregular, no un óvalo prolijo). Reintentado después de descartar los
            // guardados de terreno a mano que habían quedado desalineados la vez
            // pasada (terrain_edits.bytes / terrain_paint_detail.bytes) — ya no hay
            // diffs viejos con los que este cambio de base pueda chocar. Ruido de baja
            // frecuencia centrado en el centro de la laguna (no en coordenadas de mundo
            // crudas, para que el bulto quede pegado a la laguna sin importar dónde
            // esté el centro), agranda o achica el radio EFECTIVO según el ángulo. Se
            // usa en TODO lo que llama a LakeDist (altura, arena, barro, árboles) Y en
            // EnvironmentBuilder.BuildOrganicLakeWater (el plano de agua sigue esta
            // MISMA silueta, no un óvalo aparte), así queda consistente en todos lados.
            float bulgeNoise = Mathf.PerlinNoise(rel.x * 0.05f + 41f, rel.y * 0.05f + 17f);
            float bulge = Mathf.Lerp(0.78f, 1.28f, bulgeNoise);
            return dist / bulge;
        }
        // dCL (LakeDist) donde la altura cruza CentralLakeLevel -- el borde REAL del
        // agua (no el radio nominal, que es solo el fondo carvado). Se invierte el
        // mismo SmoothStep que usa TerrainBuilder para la rampa fondo->playa, así
        // EnvironmentBuilder pueda construir el plano de agua para que coincida
        // exactamente con la silueta orgánica que ya talla el terreno.
        public static float LakeWaterlineDist()
        {
            float k = (CentralLakeLevel - CentralLakeBed) / (CentralLakeLevel + 1f - CentralLakeBed);
            float t = 0.5f;
            for (int i = 0; i < 8; i++)
            {
                float f = 3f * t * t - 2f * t * t * t - k;
                float df = 6f * t - 6f * t * t;
                if (Mathf.Abs(df) > 1e-5f) t -= f / df;
                t = Mathf.Clamp01(t);
            }
            return CentralLakeRadius + CentralLakeBeachWidth * t;
        }
        static readonly Vector2 LakeApproachDir = (Campsite - LakeMountain).normalized;
        // true = p cae en la cuña que mira hacia el campamento (por donde llega el
        // camino/muelle) — ahí NO se cierra con árboles pegados a la orilla (owner:
        // "pon arboles toda la vuelta cerca de la orilla menos en la parte que queda
        // frente al camino"). Angostada de ~120° a ~100° (dot 0.5->0.6) -- owner
        // después: "rellena bien de arboles", menos claro abierto, más pared verde.
        public static bool LakeFacesApproach(Vector2 p)
        {
            Vector2 rel = p - CentralLakeCenter;
            if (rel.sqrMagnitude < 0.0001f) return false;
            return Vector2.Dot(rel.normalized, LakeApproachDir) > 0.6f;
        }
        // Montañas DESACOPLADAS de la laguna (owner pidió una laguna CHICA de bosque,
        // no una vista escénica de cordillera) — CentralPeakHeight=0 anula el bulto de
        // terreno; se deja el array/infra por si algún día se quiere una montaña en
        // otro punto del mapa, sin tener que rearmar este sistema desde cero.
        public static readonly Vector2[] CentralPeaks = {
            new Vector2(71f, 358f), new Vector2(49f, 354f), new Vector2(29f, 343f),
            new Vector2(15f, 326f), new Vector2(7f,  304f),
        };
        public const float CentralPeakHeight = 0f;    // 70->0: laguna chica de bosque, sin cordillera detrás (ver arriba)
        public const float CentralPeakSigma  = 30f;
        public const float SnowLine          = 82f;   // altura donde empieza la nieve en los picos
        // MONTAÑA PROCEDURAL (owner: "que las montañas sean de assets" -> se probó con un
        // modelo 3D real y quedó gigante/deforme; se vuelve a 100% procedural pero con una
        // banda de ROCA GRIS + línea de árboles reales, para que no sea una loma toda verde).
        public const float TreeLine = 56f;   // ForestBuilder no pone árboles arriba de esta altura (línea de árboles real)
        public const float RockLine = 50f;   // TerrainBuilder empieza a mezclar roca gris arriba de esta altura (llega a 100% roca en SnowLine)

        // Paths that must feel scary: narrow + dense dry forest tunnel on top
        public static readonly Vector2[][] ScaryPaths = { PathB, GraveToCriminals };

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
        public const float TunnelEntranceX   =  16f;  // x of the portal face (2ª pasada 0.75: 21→16)
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
        public static readonly Vector3 TunnelGroupOffset = new Vector3(0.11f, -15.9f, -40.8f);  // 2ª pasada 0.75 (x,z escalados; y=altura se mantiene). OJO: puede necesitar re-nudge manual
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
        public const float BaseMudBlend = 0f;      // bosque = capa 0 VERDE; el barro (capa 4 Ground071) va SOLO en caminos/campamento/rancho/galpon/cabaña

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
        // Las rondas de "demasiados arboles" (2.2/0.90 -> 2.8/0.45 -> 3.8/0.28 -> 5/0.18)
        // bajaban TreeGridStep/ForestTreeDensity GLOBAL, o sea afectaban los dos lados
        // del mapa por igual. Owner: "del lado malo (bosque/ESTE) deberian estar igual
        // que antes, la misma cantidad, vuelvelo para atras, no los toques" -- asi que
        // el grid vuelve a su valor original (2.2, el que necesita el bosque denso) y
        // ForestTreeDensity vuelve a 0.90 (ESTE, sin tocar). El campo (OESTE) ahora usa
        // su PROPIA densidad (CampoTreeDensity), calibrada a la MISMA "cantidad final"
        // que ya se habia ajustado antes (0.18 a grid 5) pero al grid mas fino de
        // ahora: 0.18 * (2.2/5)^2 ≈ 0.035. Así ajustar uno no vuelve a mover el otro.
        public const float TreeGridStep         = 2.2f;   // meters between candidate tree slots (tighter = more trees)
        public const float ScaryPathTreeDensity = 0.92f;  // closed dark tunnel (Path B & criminal territory)
        public const float PathATreeDensity     = 0.82f;  // green tunnel - also covers right up to path edges now
        public const float ForestTreeDensity    = 0.90f;  // bosque ESTE — revertido a como estaba, no tocar sin pedido explicito
        // El chequeo de distancia minima (CampoTreeMinSpacing, ver abajo) YA evita el
        // amontonamiento por si solo -- una vez que existe ESE limite, subir esta
        // densidad no vuelve a apilar arboles (el espaciado gana), solo llena mejor los
        // huecos. 0.035 era la densidad SIN el chequeo de espaciado; con el chequeo
        // sumado encima quedo doblemente restringido y salio "todo despoblado" (owner).
        public const float CampoTreeDensity     = 1f;     // campo OESTE — al maximo (0.035 -> 0.65 -> 0.9 -> 1), owner: "pobles bastante mas... de por si"
        // Distancia minima entre dos arboles de campo (owner: "quedaron muchos arboles
        // apilados no deberia pasar eso"): la densidad baja no evita que dos slots
        // VECINOS del grid (2.2m + jitter) caigan mas cerca entre si que el radio real
        // de sus copas agrandadas -> se ven pisados uno contra el otro. Este chequeo
        // (ScatterTrees) descarta un slot nuevo si ya hay un árbol de campo mas cerca.
        // A 12m, este limite (no la densidad) terminaba siendo el que de verdad
        // controlaba cuantos arboles entraban -- subir CampoTreeDensity casi no
        // sumaba mas porque casi todo el "hueco" disponible cada 12m ya se llenaba.
        // Owner: "sigue muy vacio"/"siguen habiendo muy pocos"/"pobles bastante mas
        // alrededor del campamento y de por si" -> bajado de nuevo, 12->7->5->3.5m
        // (las copas se tocan bastante entre si, pero el tronco/base no se clava
        // adentro de otro tronco como con el bug original de pisado).
        // Owner, después de sacar el pack nuevo y quedar solo 2 especies StarkCrafts:
        // "quedo un poco vacio... rellena con los arboles que estan ahora" -- con
        // CampoTreeDensity ya al máximo (1f), este espaciado es el único margen que
        // queda para meter más, 3.5->2.8.
        public const float CampoTreeMinSpacing  = 2.8f;
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
