#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// One-shot helper: builds URP/Lit materials from the Substance-style texture sets in the
// pokedex textures folder (BaseColor + Normal + Emissive). The OBJ ships with generic VRay
// slot names and no .mtl, so slot->material matching is still done by eye in the importer —
// this just removes the per-texture wiring drudgery. Run: Tools > Pokedex > Build Materials.
public static class PokedexMaterialBuilder
{
    private const string TexDir = "Assets/Pokedex/pokemon-pokedex/textures";
    private const string OutDir = "Assets/Pokedex/pokemon-pokedex/materials";
    private const string Prefix = "Pokedex_Final_";

    [MenuItem("Tools/Pokedex/Build Materials From Textures")]
    public static void Build()
    {
        if (!Directory.Exists(TexDir))
        {
            Debug.LogError($"[PokedexMaterialBuilder] Texture folder not found: {TexDir}");
            return;
        }
        if (!Directory.Exists(OutDir))
        {
            Directory.CreateDirectory(OutDir);
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[PokedexMaterialBuilder] URP Lit shader not found — is URP installed?");
            return;
        }

        // group -> (base, normal, emissive) texture paths
        var groups = new Dictionary<string, (string baseTex, string normal, string emissive)>();
        foreach (var path in Directory.GetFiles(TexDir, "*.png"))
        {
            var file = Path.GetFileNameWithoutExtension(path);
            if (!file.StartsWith(Prefix)) continue;
            var body = file.Substring(Prefix.Length);
            int u = body.LastIndexOf('_');
            if (u < 0) continue;
            var group = body.Substring(0, u);
            var suffix = body.Substring(u + 1);
            var unityPath = path.Replace('\\', '/');

            groups.TryGetValue(group, out var g);
            if (suffix.Contains("BaseColor")) g.baseTex = unityPath;
            else if (suffix.Contains("Normal")) g.normal = unityPath;
            else if (suffix.Contains("Emissive")) g.emissive = unityPath;
            // OcclusionRoughnessMetal is a packed ORM map; URP Lit can't consume it cleanly,
            // so it's intentionally skipped. Tune Metallic/Smoothness by hand if needed.
            groups[group] = g;
        }

        int made = 0;
        foreach (var kvp in groups)
        {
            var g = kvp.Value;
            MarkAsNormal(g.normal);

            var mat = new Material(shader) { name = kvp.Key };
            if (g.baseTex != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture>(g.baseTex));
            if (g.normal != null)
            {
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture>(g.normal));
                mat.EnableKeyword("_NORMALMAP");
            }
            if (g.emissive != null)
            {
                mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture>(g.emissive));
                mat.SetColor("_EmissionColor", Color.white);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mat.SetFloat("_Smoothness", 0.5f);

            AssetDatabase.CreateAsset(mat, $"{OutDir}/{kvp.Key}.mat");
            made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PokedexMaterialBuilder] Built {made} materials in {OutDir}. Now drag them onto the OBJ's material slots (Inspector > Materials).");
    }

    private static void MarkAsNormal(string path)
    {
        if (path == null) return;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }
}
#endif
