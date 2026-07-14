// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarBuilder.cs — Renault 12 PROCEDURAL (estilo PS1 low-poly).
//  Fase 1: modelo + spawn. Carrocería por cajas, ruedas como
//  hijos separados (para girarlas en Fase 2), vidrios, paragolpes
//  cromados, faros y luces traseras (emisivos). El controller de
//  manejo / entrar-salir / radio se agrega en las fases siguientes.
// ============================================================
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class CarBuilder
    {
        // Colores del R12 (blanco tiza clásico; fácil de cambiar acá).
        static readonly Color BodyColor   = new Color(0.86f, 0.86f, 0.83f);
        static readonly Color GlassColor   = new Color(0.05f, 0.06f, 0.09f);
        static readonly Color TireColor    = new Color(0.045f, 0.045f, 0.05f);
        static readonly Color ChromeColor  = new Color(0.62f, 0.63f, 0.65f);
        static readonly Color HeadColor    = new Color(1.0f, 0.95f, 0.8f);
        static readonly Color TailColor    = new Color(0.7f, 0.05f, 0.05f);

        public static GameObject Build(Transform parent, Terrain terrain)
        {
            // Estacionado en el campamento (llegaron en auto por el camino de tierra).
            Vector2 xz = MapLayout.Campsite + new Vector2(9f, -7f);
            Vector3 groundPos = BuilderUtils.Ground(terrain, xz.x, xz.y);

            var car = new GameObject("Renault12");
            car.transform.SetParent(parent);
            car.transform.position = Vector3.zero;   // se arma en el origen, se mueve al final
            car.transform.rotation = Quaternion.identity;

            BuildBody(car.transform);

            // Collider de cuerpo (sólido para el jugador) — el Rigidbody/controller va en Fase 2.
            var col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.70f, 0f);
            col.size   = new Vector3(1.60f, 1.05f, 4.05f);

            // Apoyar sobre el piso y orientar a lo largo del claro.
            car.transform.position = groundPos + Vector3.up * 0.02f;
            car.transform.rotation = Quaternion.Euler(0f, 205f, 0f);
            return car;
        }

        // ---- materiales ----
        static Material Body()   => BuilderUtils.Mat("car_body",   BodyColor);
        static Material Glass()  => BuilderUtils.Mat("car_glass",  GlassColor);
        static Material Tire()   => BuilderUtils.Mat("car_tire",   TireColor);
        static Material Chrome() => BuilderUtils.Mat("car_chrome", ChromeColor);
        static Material Head()   => BuilderUtils.Mat("car_head",   HeadColor, 1.4f); // emisivo
        static Material Tail()   => BuilderUtils.Mat("car_tail",   TailColor, 1.2f); // emisivo

        // ---- helpers de forma ----
        static GameObject Box(Transform p, string n, Vector3 pos, Vector3 size, Material m, Vector3? euler = null)
            => BuilderUtils.Prim(PrimitiveType.Cube, n, p, pos, size, m, euler);

        // Rueda: cilindro acostado sobre el eje X (rotado 90° en Z). Ø0.60, ancho 0.20.
        static GameObject Wheel(Transform p, string n, Vector3 pos)
            => BuilderUtils.Prim(PrimitiveType.Cylinder, n, p, pos, new Vector3(0.60f, 0.10f, 0.60f),
                                 Tire(), new Vector3(0f, 0f, 90f));

        static void BuildBody(Transform car)
        {
            var body = Body(); var glass = Glass(); var chrome = Chrome();

            // --- RUEDAS (hijos separados, nombrados para la Fase 2) ---
            // radio 0.30 → centro en y=0.30 apoya en el piso (y=0).
            Wheel(car, "Wheel_FL", new Vector3( 0.70f, 0.30f,  1.30f));
            Wheel(car, "Wheel_FR", new Vector3(-0.70f, 0.30f,  1.30f));
            Wheel(car, "Wheel_RL", new Vector3( 0.70f, 0.30f, -1.30f));
            Wheel(car, "Wheel_RR", new Vector3(-0.70f, 0.30f, -1.30f));

            // --- CARROCERÍA (3 volúmenes: capó / cabina / baúl) ---
            Box(car, "Body_Lower", new Vector3(0f, 0.55f,  0.00f), new Vector3(1.55f, 0.42f, 4.00f), body); // caja principal
            Box(car, "Hood",       new Vector3(0f, 0.78f,  1.35f), new Vector3(1.50f, 0.12f, 1.25f), body); // capó plano
            Box(car, "Cabin",      new Vector3(0f, 1.02f, -0.05f), new Vector3(1.42f, 0.46f, 1.95f), body); // habitáculo
            Box(car, "Roof",       new Vector3(0f, 1.27f, -0.10f), new Vector3(1.30f, 0.10f, 1.65f), body); // techo
            Box(car, "Trunk",      new Vector3(0f, 0.80f, -1.55f), new Vector3(1.50f, 0.10f, 0.95f), body); // tapa de baúl

            // --- VIDRIOS (oscuros) ---
            Box(car, "Windshield", new Vector3(0f, 1.12f,  0.92f), new Vector3(1.30f, 0.55f, 0.06f), glass, new Vector3( 30f, 0f, 0f));
            Box(car, "RearWindow", new Vector3(0f, 1.12f, -1.05f), new Vector3(1.30f, 0.50f, 0.06f), glass, new Vector3(-28f, 0f, 0f));
            Box(car, "SideWin_L",  new Vector3( 0.71f, 1.12f, -0.05f), new Vector3(0.05f, 0.34f, 1.50f), glass);
            Box(car, "SideWin_R",  new Vector3(-0.71f, 1.12f, -0.05f), new Vector3(0.05f, 0.34f, 1.50f), glass);

            // --- PARAGOLPES + PARRILLA (cromo) ---
            Box(car, "Bumper_F", new Vector3(0f, 0.48f,  2.05f), new Vector3(1.55f, 0.18f, 0.16f), chrome);
            Box(car, "Bumper_R", new Vector3(0f, 0.48f, -2.05f), new Vector3(1.55f, 0.18f, 0.16f), chrome);
            Box(car, "Grille",   new Vector3(0f, 0.66f,  2.00f), new Vector3(1.20f, 0.22f, 0.06f), Glass());

            // --- FAROS (emisivos) — nombrados para prender/apagar en Fase 3 ---
            Box(car, "Headlight_L", new Vector3( 0.55f, 0.68f, 2.01f), new Vector3(0.28f, 0.16f, 0.05f), Head());
            Box(car, "Headlight_R", new Vector3(-0.55f, 0.68f, 2.01f), new Vector3(0.28f, 0.16f, 0.05f), Head());

            // --- LUCES TRASERAS (emisivas rojas) ---
            Box(car, "Taillight_L", new Vector3( 0.60f, 0.74f, -2.03f), new Vector3(0.26f, 0.18f, 0.05f), Tail());
            Box(car, "Taillight_R", new Vector3(-0.60f, 0.74f, -2.03f), new Vector3(0.26f, 0.18f, 0.05f), Tail());
        }
    }
}
