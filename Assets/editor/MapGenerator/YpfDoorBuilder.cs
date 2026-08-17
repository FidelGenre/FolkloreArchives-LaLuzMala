// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  YpfDoorBuilder.cs — owner: "las puertas de la YPF y la del baño se puedan
//  abrir". Busca los objetos-puerta del edificio YPF (Door_01/02/03, Puerta*)
//  y del baño (Toilet_cubicles*) y les agrega SwingDoor (abre/cierra con E).
//  EXCLUYE las puertas del AUTO (L_Door_*/R_Door_*). Se llama desde
//  MapGenerator.Generate.
// ============================================================
using System.Text.RegularExpressions;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class YpfDoorBuilder
    {
        // raíces de puerta (nombre exacto, sin sufijos de sub-malla como _Glass/_Metal/_Doo)
        static readonly Regex DoorName = new Regex(
            @"^(door(_0[0-9])?|puerta(\.[0-9]+)?|toilet_cubicles(_[0-9]+)?)$",
            RegexOptions.IgnoreCase);

        public static void Build(Transform root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string low = t.name.ToLowerInvariant();
                if (low.Contains("l_door") || low.Contains("r_door")) continue; // puertas del AUTO, no
                if (!DoorName.IsMatch(low)) continue;
                if (t.GetComponent<FolkloreArchives.SwingDoor>() != null) continue;
                // NO estática (a la puerta y todos sus hijos): si queda estática, Unity la
                // batchea y aunque rote no se mueve visualmente (bug de las 2 que no abrían).
                foreach (var tr in t.GetComponentsInChildren<Transform>(true))
                    tr.gameObject.isStatic = false;
                t.gameObject.AddComponent<FolkloreArchives.SwingDoor>();
                n++;
            }
            if (n > 0) Debug.Log($"<color=lime>[YpfDoor] {n} puertas de la YPF/baño ahora abren con E.</color>");
            else Debug.LogWarning("[YpfDoor] no encontré puertas para hacer abribles (¿nombres distintos?).");
        }
    }
}
