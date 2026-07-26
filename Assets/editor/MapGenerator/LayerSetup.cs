// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  LayerSetup.cs — asegura que exista un layer de usuario dado
//  (Project Settings > Tags and Layers no tiene API pública para
//  agregar layers en runtime; hay que tocar el asset directamente).
//  Se usa para "SelfHidden": el perro sentado en el auto se pone en
//  este layer y su propia cámara lo excluye de su cullingMask -- así
//  no se ve a sí mismo, pero los demás jugadores lo siguen viendo
//  normal (el layer es una propiedad LOCAL, no sincronizada por red,
//  así que cada cliente decide qué renderiza su propia cámara).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class LayerSetup
    {
        public const string SelfHiddenLayer = "SelfHidden";

        // Devuelve el índice del layer, creándolo en el primer slot de usuario libre
        // (8..31) si todavía no existe. Llamar durante Generate (antes de Play).
        public static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = name;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[LayerSetup] Creado layer '{name}' en el slot {i}.");
                    return i;
                }
            }
            Debug.LogWarning($"[LayerSetup] No quedan slots de layer libres para '{name}'.");
            return -1;
        }
    }
}
