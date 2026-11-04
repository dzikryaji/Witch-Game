using System.IO;
using UnityEditor;
using UnityEngine;

namespace Witch.EditorTools
{
    public static class RoundedCardSpriteGenerator
    {
        private const int Size = 64;
        private const int Radius = 16;
        private const string AssetPath = "Assets/Art/UI/Rewind/RoundedCard.png";

        [MenuItem("Witch/Rewind/Create Rounded Card Sprite")]
        public static void Create()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, CoverageAt(x, y)));
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            File.WriteAllBytes(AssetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(AssetPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(Radius, Radius, Radius, Radius);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        private static float CoverageAt(int x, int y)
        {
            float px = x + 0.5f;
            float py = y + 0.5f;
            float cx = Mathf.Clamp(px, Radius, Size - Radius);
            float cy = Mathf.Clamp(py, Radius, Size - Radius);
            float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01(Radius - distance + 0.5f);
        }
    }
}