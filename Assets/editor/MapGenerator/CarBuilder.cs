// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarBuilder.cs — Renault 12 PROCEDURAL (estilo PS1 low-poly).
//  Fase 1: modelo + spawn en la RUTA pasando el túnel. Perfil de
//  sedán (capó largo y bajo, techo corto hacia atrás, baúl corto),
//  ruedas como hijos separados (para girarlas en Fase 2), vidrios,
//  paragolpes cromados, faros y luces traseras (emisivos).
// ============================================================
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class CarBuilder
    {
        // Colores del R12 (blanco tiza clásico; fácil de cambiar acá).
        static readonly Color BodyColor  = new Color(0.86f, 0.86f, 0.83f);
        static readonly Color GlassColor = new Color(0.05f, 0.06f, 0.09f);
        static readonly Color TireColor  = new Color(0.045f, 0.045f, 0.05f);
        static readonly Color ChromeColor= new Color(0.62f, 0.63f, 0.65f);
        static readonly Color HeadColor  = new Color(1.0f, 0.95f, 0.8f);
        static readonly Color TailColor  = new Color(0.7f, 0.05f, 0.05f);

        public static GameObject Build(Transform parent, Terrain terrain)
        {
            // En la RUTA, ~25m pasando el portal del túnel (el jugador sale hacia el este).
            float carX = MapLayout.TunnelEntranceX + 25f;
            float carZ = MapLayout.PavedRouteZAt(carX);
            var pos = new Vector3(carX, MapLayout.RoadSurfaceHeight, carZ);

            // Alinear con la dirección de la ruta (tangente), mirando hacia el este (+X, afuera).
            float dz = MapLayout.PavedRouteZAt(carX + 6f) - MapLayout.PavedRouteZAt(carX - 6f);
            float yaw = Mathf.Atan2(12f, dz) * Mathf.Rad2Deg;

            var car = new GameObject("Renault12");
            car.transform.SetParent(parent);
            car.transform.position = Vector3.zero;   // se arma en el origen, se mueve al final
            car.transform.rotation = Quaternion.identity;

            BuildBody(car.transform);

            var col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.68f, 0f);
            col.size   = new Vector3(1.60f, 1.05f, 4.25f);

            car.transform.position = pos;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return car;
        }

        // ---- materiales ----
        static Material Body()   => BuilderUtils.Mat("car_body",   BodyColor);
        static Material Glass()  => BuilderUtils.Mat("car_glass",  GlassColor);
        static Material Tire()   => BuilderUtils.Mat("car_tire",   TireColor);
        static Material Chrome() => BuilderUtils.Mat("car_chrome", ChromeColor);
        static Material Head()   => BuilderUtils.Mat("car_head",   HeadColor, 1.4f);
        static Material Tail()   => BuilderUtils.Mat("car_tail",   TailColor, 1.2f);

        // ---- helpers de forma ----
        static GameObject Box(Transform p, string n, Vector3 pos, Vector3 size, Material m, Vector3? euler = null)
            => BuilderUtils.Prim(PrimitiveType.Cube, n, p, pos, size, m, euler);

        // Rueda: cilindro acostado sobre el eje X (rotado 90° en Z). Ø0.60, ancho 0.20.
        static GameObject Wheel(Transform p, string n, Vector3 pos)
            => BuilderUtils.Prim(PrimitiveType.Cylinder, n, p, pos, new Vector3(0.60f, 0.10f, 0.60f),
                                 Tire(), new Vector3(0f, 0f, 90f));

        // +Z = adelante (trompa). Perfil de sedán R12: capó largo y bajo, cintura,
        // techo corto corrido hacia atrás, baúl corto.
        static void BuildBody(Transform car)
        {
            var body = Body(); var glass = Glass(); var chrome = Chrome();

            // --- RUEDAS (hijos separados, nombrados para la Fase 2) ---
            Wheel(car, "Wheel_FL", new Vector3( 0.72f, 0.30f,  1.25f));
            Wheel(car, "Wheel_FR", new Vector3(-0.72f, 0.30f,  1.25f));
            Wheel(car, "Wheel_RL", new Vector3( 0.72f, 0.30f, -1.25f));
            Wheel(car, "Wheel_RR", new Vector3(-0.72f, 0.30f, -1.25f));

            // --- CARROCERÍA ---
            // zócalo bajo a lo largo de todo el auto
            Box(car, "Sill",  new Vector3(0f, 0.50f,  0.00f), new Vector3(1.56f, 0.34f, 4.20f), body);
            // capó LARGO y bajo (trompa) — clave del look R12
            Box(car, "Hood",  new Vector3(0f, 0.72f,  1.35f), new Vector3(1.52f, 0.13f, 1.55f), body);
            // laterales/puertas subiendo hasta la cintura (solo medio + cola, la trompa queda baja)
            Box(car, "Sides", new Vector3(0f, 0.73f, -0.55f), new Vector3(1.56f, 0.44f, 3.05f), body);
            // baúl corto (tapa plana a la altura de la cintura)
            Box(car, "Trunk", new Vector3(0f, 0.90f, -1.65f), new Vector3(1.52f, 0.12f, 0.90f), body);
            // techo CORTO y corrido hacia atrás (deja capó adelante y baúl atrás)
            Box(car, "Roof",  new Vector3(0f, 1.29f, -0.35f), new Vector3(1.28f, 0.10f, 1.45f), body);

            // --- VIDRIOS (oscuros, parabrisas y luneta inclinados) ---
            Box(car, "Windshield", new Vector3(0f, 1.10f,  0.64f), new Vector3(1.28f, 0.44f, 0.06f), glass, new Vector3( 34f, 0f, 0f));
            Box(car, "RearWindow", new Vector3(0f, 1.10f, -1.28f), new Vector3(1.28f, 0.38f, 0.06f), glass, new Vector3(-32f, 0f, 0f));
            Box(car, "SideWin_L",  new Vector3( 0.72f, 1.10f, -0.35f), new Vector3(0.05f, 0.28f, 1.30f), glass);
            Box(car, "SideWin_R",  new Vector3(-0.72f, 1.10f, -0.35f), new Vector3(0.05f, 0.28f, 1.30f), glass);
            // parante central (B) — parte la ventanilla en dos como el R12
            Box(car, "Pillar_L",   new Vector3( 0.73f, 1.08f, -0.35f), new Vector3(0.04f, 0.32f, 0.10f), body);
            Box(car, "Pillar_R",   new Vector3(-0.73f, 1.08f, -0.35f), new Vector3(0.04f, 0.32f, 0.10f), body);

            // --- PARAGOLPES + PARRILLA (cromo / oscuro) ---
            Box(car, "Bumper_F", new Vector3(0f, 0.46f,  2.14f), new Vector3(1.55f, 0.16f, 0.16f), chrome);
            Box(car, "Bumper_R", new Vector3(0f, 0.46f, -2.14f), new Vector3(1.55f, 0.16f, 0.16f), chrome);
            Box(car, "Grille",   new Vector3(0f, 0.60f,  2.10f), new Vector3(1.24f, 0.20f, 0.05f), glass);

            // --- FAROS (emisivos) — nombrados para prender/apagar en Fase 3 ---
            Box(car, "Headlight_L", new Vector3( 0.55f, 0.63f, 2.11f), new Vector3(0.28f, 0.15f, 0.05f), Head());
            Box(car, "Headlight_R", new Vector3(-0.55f, 0.63f, 2.11f), new Vector3(0.28f, 0.15f, 0.05f), Head());

            // --- LUCES TRASERAS (emisivas rojas) ---
            Box(car, "Taillight_L", new Vector3( 0.60f, 0.70f, -2.12f), new Vector3(0.26f, 0.18f, 0.05f), Tail());
            Box(car, "Taillight_R", new Vector3(-0.60f, 0.70f, -2.12f), new Vector3(0.26f, 0.18f, 0.05f), Tail());
        }
    }
}
