// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarBuilder.cs — Renault 12 PROCEDURAL con MALLA LOFT (no cajas).
//  La carrocería se genera por secciones transversales redondeadas
//  a lo largo del auto siguiendo el perfil del R12 (trompa baja,
//  capó largo, parabrisas, techo corto atrás, luneta, baúl). Los
//  vidrios van INTEGRADOS en la malla (submesh aparte, oscuro), así
//  la silueta queda suave y "real", no de bloques. Ruedas + hubcaps,
//  paragolpes, faros y luces traseras como piezas chicas. Spawn en
//  la ruta pasando el túnel.
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class CarBuilder
    {
        static readonly Color BodyColor  = new Color(0.86f, 0.86f, 0.83f);
        static readonly Color GlassColor = new Color(0.05f, 0.06f, 0.09f);
        static readonly Color TireColor  = new Color(0.045f, 0.045f, 0.05f);
        static readonly Color ChromeColor= new Color(0.62f, 0.63f, 0.65f);
        static readonly Color HeadColor  = new Color(1.0f, 0.95f, 0.8f);
        static readonly Color TailColor  = new Color(0.7f, 0.05f, 0.05f);

        const float FloorY = 0.34f;   // parte de abajo de la carrocería

        // Secciones a lo largo del auto (z: +adelante). Wbot = medio ancho abajo,
        // Wtop = medio ancho arriba (techo/greenhouse más angosto = tumblehome),
        // roofY = altura del contorno superior en esa estación (el PERFIL del R12).
        struct Station { public float z, Wbot, Wtop, roofY; }
        static readonly Station[] S = new Station[]
        {
            new Station{z= 2.10f, Wbot=0.62f, Wtop=0.55f, roofY=0.58f}, // trompa (baja, angosta)
            new Station{z= 1.90f, Wbot=0.76f, Wtop=0.71f, roofY=0.79f}, // frente del capó
            new Station{z= 1.15f, Wbot=0.79f, Wtop=0.75f, roofY=0.81f}, // capó (largo y plano)
            new Station{z= 0.68f, Wbot=0.79f, Wtop=0.72f, roofY=0.96f}, // base del parabrisas
            new Station{z= 0.30f, Wbot=0.79f, Wtop=0.62f, roofY=1.26f}, // arriba del parabrisas
            new Station{z=-0.25f, Wbot=0.79f, Wtop=0.60f, roofY=1.34f}, // techo (medio)
            new Station{z=-0.85f, Wbot=0.79f, Wtop=0.61f, roofY=1.31f}, // techo (atrás)
            new Station{z=-1.28f, Wbot=0.78f, Wtop=0.68f, roofY=1.02f}, // base de la luneta
            new Station{z=-1.72f, Wbot=0.77f, Wtop=0.73f, roofY=0.90f}, // baúl
            new Station{z=-2.06f, Wbot=0.66f, Wtop=0.60f, roofY=0.76f}, // cola
        };

        public static GameObject Build(Transform parent, Terrain terrain)
        {
            float carX = MapLayout.TunnelEntranceX + 25f;          // en la ruta, pasando el túnel
            float carZ = MapLayout.PavedRouteZAt(carX);
            var pos = new Vector3(carX, MapLayout.RoadSurfaceHeight, carZ);
            float dz = MapLayout.PavedRouteZAt(carX + 6f) - MapLayout.PavedRouteZAt(carX - 6f);
            float yaw = Mathf.Atan2(12f, dz) * Mathf.Rad2Deg;      // mirando al este (afuera)

            var car = new GameObject("Renault12");
            car.transform.SetParent(parent);
            car.transform.position = Vector3.zero;
            car.transform.rotation = Quaternion.identity;

            // --- Carrocería: malla loft (body + glass en submeshes) ---
            var shell = new GameObject("Shell");
            shell.transform.SetParent(car.transform);
            shell.transform.localPosition = Vector3.zero;
            var mf = shell.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildBodyMesh();
            var mr = shell.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { Body(), Glass() };

            BuildWheels(car.transform);
            BuildDetails(car.transform);

            var col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.70f, 0f);
            col.size   = new Vector3(1.62f, 1.10f, 4.30f);

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

        // ---- malla de la carrocería (loft de secciones redondeadas) ----
        static Vector3[] Ring(Station st)
        {
            float bx = st.Wbot, tx = st.Wtop, ry = st.roofY, z = st.z;
            float by = FloorY + (ry - FloorY) * 0.5f;   // línea de cintura
            // 12 puntos, en sentido horario visto de frente: abajo-centro → derecha →
            // arriba → izquierda → abajo. Índices fijos para saber cuáles son "vidrio".
            return new Vector3[]
            {
                new Vector3(0f,            FloorY,        z), // 0 abajo-centro
                new Vector3(0.55f*bx,      FloorY,        z), // 1
                new Vector3(bx,            FloorY+0.10f,  z), // 2 esquina inferior
                new Vector3(bx,            by,            z), // 3 cintura (der)
                new Vector3(tx,            ry-0.12f,      z), // 4 hombro (der)   <- vidrio lateral
                new Vector3(0.82f*tx,      ry,            z), // 5 techo (der)
                new Vector3(0f,            ry,            z), // 6 techo-centro
                new Vector3(-0.82f*tx,     ry,            z), // 7 techo (izq)
                new Vector3(-tx,           ry-0.12f,      z), // 8 hombro (izq)   <- vidrio lateral
                new Vector3(-bx,           by,            z), // 9 cintura (izq)
                new Vector3(-bx,           FloorY+0.10f,  z), //10 esquina inferior
                new Vector3(-0.55f*bx,     FloorY,        z), //11
            };
        }

        static Mesh BuildBodyMesh()
        {
            var verts = new List<Vector3>();
            int[] ringStart = new int[S.Length];
            for (int s = 0; s < S.Length; s++) { ringStart[s] = verts.Count; verts.AddRange(Ring(S[s])); }

            var body = new List<int>();
            var glass = new List<int>();
            for (int s = 0; s < S.Length - 1; s++)
            {
                int a = ringStart[s], b = ringStart[s + 1];
                float zmid = (S[s].z + S[s + 1].z) * 0.5f;
                bool cabin = zmid < 0.70f && zmid > -1.30f;
                bool wind  = zmid < 0.70f && zmid >  0.28f;   // pendiente del parabrisas
                bool rear  = zmid < -0.83f && zmid > -1.30f;  // pendiente de la luneta
                for (int k = 0; k < 12; k++)
                {
                    int k2 = (k + 1) % 12;
                    int A = a + k, B = a + k2, C = b + k2, D = b + k;
                    bool sideGlass = (k == 3 || k == 4 || k == 7 || k == 8);
                    bool topGlass  = (k == 5 || k == 6);
                    bool isGlass = (sideGlass && cabin && !wind && !rear) || (topGlass && (wind || rear));
                    var t = isGlass ? glass : body;
                    // hacia afuera: (A,D,C)+(A,C,B)
                    t.Add(A); t.Add(D); t.Add(C);
                    t.Add(A); t.Add(C); t.Add(B);
                }
            }
            // tapas de trompa y cola (abanico desde el centro-abajo)
            AddCap(body, ringStart[0], true);
            AddCap(body, ringStart[S.Length - 1], false);

            string path = MapLayout.GeneratedFolder + "/mesh_renault12.asset";
            var mesh = new Mesh { name = "Renault12Body" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(glass, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void AddCap(List<int> tris, int r, bool front)
        {
            for (int k = 1; k < 11; k++)
            {
                if (front) { tris.Add(r); tris.Add(r + k); tris.Add(r + k + 1); }
                else       { tris.Add(r); tris.Add(r + k + 1); tris.Add(r + k); }
            }
        }

        // ---- ruedas (neumático + tapa cromada) ----
        static void BuildWheels(Transform car)
        {
            Vector3[] w = {
                new Vector3( 0.74f, 0.31f,  1.25f), new Vector3(-0.74f, 0.31f,  1.25f),
                new Vector3( 0.74f, 0.31f, -1.25f), new Vector3(-0.74f, 0.31f, -1.25f),
            };
            string[] n = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            for (int i = 0; i < 4; i++)
            {
                var wheel = new GameObject(n[i]);
                wheel.transform.SetParent(car);
                wheel.transform.localPosition = w[i];
                wheel.transform.localRotation = Quaternion.identity;
                float outward = w[i].x > 0 ? 1f : -1f;
                // neumático (cilindro acostado sobre el eje X = eje de giro de la rueda)
                var tire = BuilderUtils.Prim(PrimitiveType.Cylinder, "Tire", wheel.transform, Vector3.zero,
                    new Vector3(0.62f, 0.10f, 0.62f), Tire(), new Vector3(0f, 0f, 90f));
                tire.transform.localPosition = Vector3.zero;
                // tapa cromada, corrida hacia el lado de afuera
                var hub = BuilderUtils.Prim(PrimitiveType.Cylinder, "Hub", wheel.transform, Vector3.zero,
                    new Vector3(0.26f, 0.02f, 0.26f), Chrome(), new Vector3(0f, 0f, 90f));
                hub.transform.localPosition = new Vector3(0.055f * outward, 0f, 0f);
            }
        }

        // ---- detalles (paragolpes, parrilla, faros, luces) ----
        static void BuildDetails(Transform car)
        {
            var chrome = Chrome(); var glass = Glass();
            Box(car, "Bumper_F", new Vector3(0f, 0.46f,  2.12f), new Vector3(1.55f, 0.16f, 0.16f), chrome);
            Box(car, "Bumper_R", new Vector3(0f, 0.46f, -2.12f), new Vector3(1.55f, 0.16f, 0.16f), chrome);
            Box(car, "Grille",   new Vector3(0f, 0.58f,  2.06f), new Vector3(1.20f, 0.20f, 0.05f), glass);
            Box(car, "Headlight_L", new Vector3( 0.55f, 0.62f, 2.05f), new Vector3(0.28f, 0.14f, 0.05f), Head());
            Box(car, "Headlight_R", new Vector3(-0.55f, 0.62f, 2.05f), new Vector3(0.28f, 0.14f, 0.05f), Head());
            Box(car, "Taillight_L", new Vector3( 0.60f, 0.72f, -2.09f), new Vector3(0.26f, 0.16f, 0.05f), Tail());
            Box(car, "Taillight_R", new Vector3(-0.60f, 0.72f, -2.09f), new Vector3(0.26f, 0.16f, 0.05f), Tail());
        }

        static void Box(Transform p, string n, Vector3 lpos, Vector3 size, Material m)
        {
            var g = BuilderUtils.Prim(PrimitiveType.Cube, n, p, lpos, size, m);
            g.transform.localPosition = lpos; // Prim setea pos absoluta; con parent en origen coincide
        }
    }
}
