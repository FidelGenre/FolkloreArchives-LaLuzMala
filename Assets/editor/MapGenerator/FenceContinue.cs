// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  FenceContinue.cs — owner armó a mano la primera fila de valla
//  (bien escalada, orientada y apoyada) y pidió "puedes completar el
//  camino faltante?". En vez de adivinar de nuevo escala/rotación
//  (los 2 intentos automáticos anteriores fallaron), este comando
//  CLONA la pieza que el owner ya dejó bien puesta y continúa la
//  fila siguiendo la curva real del camino más cercano, aprendiendo
//  de la pieza seleccionada:
//   - separación del camino (con signo, mismo lado)
//   - diferencia de yaw entre "dirección del camino ahí" y la
//     rotación real de la pieza (así no importa cuál sea el eje
//     "adelante" del mesh -- se copia la relación que ya funciona)
//   - altura relativa al terreno
//   - escala
//
//  USO: en la Hierarchy, seleccioná la ÚLTIMA pieza de la fila que
//  ya pusiste a mano (la que está más lejos siguiendo el camino), y
//  corré Tools > Folklore Archives > Continuar Valla (desde selección).
//  Sigue agregando piezas desde ahí hasta el final del camino
//  detectado (el más cercano a la pieza seleccionada, de los que ya
//  conoce: DirtRoad, Camino10).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class FenceContinue
    {
        // Arregla una pieza que quedó inclinada por accidente (arrastrando el gizmo
        // de rotación en diagonal en vez de girar solo en Y) -- deja X y Z en 0 y
        // conserva el yaw (Y) tal como estaba. Útil ANTES de Continuar Valla si esa
        // herramienta avisa que la pieza elegida está inclinada.
        [MenuItem("Tools/Folklore Archives/Enderezar rotación (solo Y)")]
        public static void StraightenSelection()
        {
            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogWarning("[FenceContinue] Seleccioná una o más piezas inclinadas primero.");
                return;
            }
            foreach (var go in Selection.gameObjects)
            {
                float y = go.transform.eulerAngles.y;
                go.transform.rotation = Quaternion.Euler(0f, y, 0f);
            }
            Debug.Log($"<color=lime>[FenceContinue] Enderezadas {Selection.gameObjects.Length} pieza(s) (X=0, Z=0, Y sin tocar).</color>");
        }

        [MenuItem("Tools/Folklore Archives/Continuar Valla (desde selección)")]
        public static void ContinueFence()
        {
            var sel = Selection.activeGameObject;
            if (sel == null)
            {
                Debug.LogWarning("[FenceContinue] Seleccioná la ÚLTIMA pieza de la fila de valla (la más lejos siguiendo el camino) antes de correr esto.");
                return;
            }
            var terrain = Terrain.activeTerrain;
            if (terrain == null) { Debug.LogWarning("[FenceContinue] No hay Terrain activo."); return; }

            // la pieza seleccionada es la "maestra" -- todo lo que tenga de raro (un
            // giro sin querer en X/Z, no solo el yaw en Y) se copia tal cual a las
            // piezas nuevas. Si está inclinada (no solo rotada de costado), mejor
            // avisar y frenar en vez de repetir el error 46 veces.
            float tiltX = Mathf.DeltaAngle(0f, sel.transform.eulerAngles.x);
            float tiltZ = Mathf.DeltaAngle(0f, sel.transform.eulerAngles.z);
            if (Mathf.Abs(tiltX) > 3f || Mathf.Abs(tiltZ) > 3f)
            {
                Debug.LogWarning($"[FenceContinue] La pieza seleccionada ('{sel.name}') está INCLINADA (rotación X={tiltX:F1}°, Z={tiltZ:F1}°, debería ser 0 en las dos) -- no sigo desde acá. " +
                                  "Con la pieza seleccionada, corré Tools > Folklore Archives > Enderezar rotación (solo Y) para arreglarla, y después volvé a correr este comando.");
                return;
            }

            Vector2 selXZ = new Vector2(sel.transform.position.x, sel.transform.position.z);

            // elegir el camino conocido más cercano a la pieza seleccionada
            (string name, Vector2[] path) best = (null, null);
            float bestDist = float.MaxValue, startArc = 0f;
            Vector2 nearestPt = Vector2.zero, tangentAtSel = Vector2.up;
            foreach (var c in new (string, Vector2[])[] { ("DirtRoad", MapLayout.DirtRoad), ("Camino10", MapLayout.Camino10Path) })
            {
                float arc = ArcLengthAt(c.Item2, selXZ, out Vector2 np, out Vector2 tg);
                float d = Vector2.Distance(selXZ, np);
                if (d < bestDist) { bestDist = d; best = c; startArc = arc; nearestPt = np; tangentAtSel = tg; }
            }
            if (best.path == null) { Debug.LogWarning("[FenceContinue] No encontré ningún camino conocido cerca de la selección."); return; }

            // separación CON SIGNO (de qué lado del camino está la pieza) — mismo lado
            // para todas las piezas nuevas.
            Vector2 perp = new Vector2(-tangentAtSel.y, tangentAtSel.x);
            float offset = Vector2.Dot(selXZ - nearestPt, perp);

            // diferencia entre el yaw real de la pieza y la dirección del camino ahí —
            // se copia tal cual para las piezas nuevas, sea cual sea el eje "adelante"
            // real del mesh (ya funciona en la pieza seleccionada, no hace falta saberlo).
            float tangentYawAtSel = Mathf.Atan2(tangentAtSel.x, tangentAtSel.y) * Mathf.Rad2Deg;
            float yawOffset = sel.transform.eulerAngles.y - tangentYawAtSel;

            // altura relativa al terreno (por si la pieza no está exactamente pegada al piso)
            float relY = sel.transform.position.y - terrain.SampleHeight(sel.transform.position);

            // separación entre piezas = tamaño real (mundo) de la pieza ya puesta.
            var rends = sel.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Debug.LogWarning("[FenceContinue] La selección no tiene ningún Renderer."); return; }
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float segLen = Mathf.Max(0.3f, Mathf.Max(b.size.x, b.size.z));

            float totalArc = TotalLength(best.path);
            int count = Mathf.FloorToInt((totalArc - startArc) / segLen);
            if (count < 1) { Debug.Log("[FenceContinue] La selección ya está pegada al final del camino (" + best.name + "), no hay más para agregar."); return; }

            var parent = sel.transform.parent;
            int made = 0;
            for (int i = 1; i <= count; i++)
            {
                float arc = startArc + i * segLen;
                Vector2 pos = PointAtArc(best.path, arc, out Vector2 tangent);
                Vector2 finalXZ = pos + new Vector2(-tangent.y, tangent.x) * offset;
                float yaw = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg + yawOffset;

                var clone = Object.Instantiate(sel, parent);
                clone.name = sel.name + "_cont" + made++;
                float y = terrain.SampleHeight(new Vector3(finalXZ.x, 0f, finalXZ.y)) + relY;
                clone.transform.position = new Vector3(finalXZ.x, y, finalXZ.y);
                clone.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            Debug.Log($"<color=lime>[FenceContinue] Agregadas {made} piezas más siguiendo {best.name}, desde la selección hasta el final del camino.</color>");
        }

        // Longitud acumulada a lo largo del camino hasta el punto más cercano a p.
        static float ArcLengthAt(Vector2[] path, Vector2 p, out Vector2 nearestPt, out Vector2 tangent)
        {
            float acc = 0f, bestArc = 0f, bestDist = float.MaxValue;
            nearestPt = path[0]; tangent = Vector2.up;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], c = path[i + 1];
                Vector2 ac = c - a;
                float len2 = ac.sqrMagnitude;
                float segLen = Mathf.Sqrt(len2);
                float t = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ac) / len2) : 0f;
                Vector2 proj = a + ac * t;
                float d = Vector2.Distance(p, proj);
                if (d < bestDist)
                {
                    bestDist = d; bestArc = acc + segLen * t;
                    nearestPt = proj; tangent = len2 > 0.0001f ? ac.normalized : Vector2.up;
                }
                acc += segLen;
            }
            return bestArc;
        }

        static float TotalLength(Vector2[] path)
        {
            float total = 0f;
            for (int i = 0; i < path.Length - 1; i++) total += Vector2.Distance(path[i], path[i + 1]);
            return total;
        }

        // Punto (y tangente del segmento que lo contiene) a una distancia recorrida
        // "arc" desde el principio del camino.
        static Vector2 PointAtArc(Vector2[] path, float arc, out Vector2 tangent)
        {
            float acc = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], c = path[i + 1];
                float segLen = Vector2.Distance(a, c);
                if (acc + segLen >= arc || i == path.Length - 2)
                {
                    float t = segLen > 0.0001f ? Mathf.Clamp01((arc - acc) / segLen) : 0f;
                    tangent = segLen > 0.0001f ? (c - a).normalized : Vector2.up;
                    return Vector2.Lerp(a, c, t);
                }
                acc += segLen;
            }
            tangent = Vector2.up;
            return path[path.Length - 1];
        }
    }
}
