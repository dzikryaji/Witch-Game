using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Witch.Infrastructure.Audio;

namespace Witch.Editor
{
    public static class OriginalAudioGenerator
    {
        private const int SampleRate = 22050;
        private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        private const string BgmFolder = "Assets/Audio/BGM";
        private const string SfxFolder = "Assets/Audio/SFX";

        private static readonly KeyValuePair<string, string>[] Cues =
        {
            Pair("BGM_MAIN_MENU", BgmFolder + "/AlkaKrab/Medieval Vol. 2 1 (Loop).ogg"),
            Pair("BGM_SHOP", BgmFolder + "/AlkaKrab/Medieval Vol. 2 2 (Loop).ogg"),
            Pair("BGM_REWIND_MINIGAME", BgmFolder + "/AlkaKrab/Medieval Vol. 2 3 (Loop).ogg"),
            Pair("BGM_GAME_OVER", BgmFolder + "/AlkaKrab/Medieval Vol. 2 5 (Loop).ogg"),
            Pair("BGM_GAME_COMPLETE", BgmFolder + "/AlkaKrab/Medieval Vol. 2 6 (Loop).ogg"),
            Pair("SFX_SHOP_AMBIENCE", SfxFolder + "/ShopAmbience.ogg"),
            Pair("SFX_CUSTOMER_BELL", SfxFolder + "/CustomerBell.mp3"),
            Pair("SFX_PAGE_FLIP", SfxFolder + "/PageFlip.mp3"),
            Pair("SFX_GRINDER", SfxFolder + "/Grinder.wav"),
            Pair("SFX_HERB_PLUCK", SfxFolder + "/HerbPluck.wav"),
            Pair("SFX_BREW_SUCCESS", SfxFolder + "/BrewSuccess.wav"),
            Pair("SFX_DISCARD", SfxFolder + "/Discard.wav"),
            Pair("SFX_ORDER_CORRECT", SfxFolder + "/OrderCorrect_Original.wav"),
            Pair("SFX_PAYMENT_COINS", SfxFolder + "/Coins_Original.wav"),
            Pair("SFX_ORDER_WRONG", SfxFolder + "/OrderWrong_Original.wav"),
            Pair("SFX_CARD_MATCH", SfxFolder + "/CardMatch.wav"),
            Pair("SFX_CARD_MISMATCH", SfxFolder + "/CardMismatch.wav"),
            Pair("SFX_MINIGAME_COMPLETE", SfxFolder + "/MinigameComplete_Original.wav"),
            Pair("SFX_EXPENSE_PAY", SfxFolder + "/ExpensePay_Original.wav"),
            Pair("SFX_EXPENSE_FAIL", SfxFolder + "/ExpenseFail_Original.wav"),
            Pair("SFX_UI_CLICK", SfxFolder + "/UiClick.ogg")
        };

        [MenuItem("Witch/Audio/Generate Original Audio and Bind Bootstrap")]
        public static void GenerateAndBind()
        {
            Directory.CreateDirectory(BgmFolder);
            Directory.CreateDirectory(SfxFolder);

            foreach (KeyValuePair<string, string> cue in Cues)
            {
                if (!cue.Value.EndsWith("_Original.wav", StringComparison.OrdinalIgnoreCase)) continue;
                float[] samples = CreateEffect(cue.Key);
                WriteWave(cue.Value, samples);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporters();
            BindBootstrap();
            Debug.Log("Generated and bound " + Cues.Length + " original audio clips.");
        }

        private static float[] CreateEffect(string cueId)
        {
            float duration = EffectDuration(cueId);
            var result = new float[Mathf.CeilToInt(SampleRate * duration)];
            var random = new System.Random(StableHash(cueId));
            for (int index = 0; index < result.Length; index++)
            {
                float time = index / (float)SampleRate;
                float progress = time / duration;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float value;
                switch (cueId)
                {
                    case "SFX_PAYMENT_COINS":
                    case "SFX_EXPENSE_PAY":
                        value = CoinCluster(time, duration);
                        break;
                    case "SFX_BREW_SUCCESS":
                    case "SFX_ORDER_CORRECT":
                    case "SFX_MINIGAME_COMPLETE":
                        value = Arpeggio(time, duration, new[] { 60, 64, 67, 72 });
                        break;
                    case "SFX_ORDER_WRONG":
                    case "SFX_EXPENSE_FAIL":
                        value = Arpeggio(time, duration, new[] { 55, 51, 48, 43 });
                        break;
                    default:
                        value = Wave(440f, time) * (1f - progress) * 0.2f;
                        break;
                }
                result[index] = value;
            }
            FadeEdges(result, 0.005f);
            return result;
        }

        private static float CoinCluster(float time, float duration)
        {
            float value = 0f;
            float[] starts = { 0f, 0.11f, 0.23f, 0.36f };
            float[] frequencies = { 1420f, 1760f, 1530f, 1920f };
            for (int index = 0; index < starts.Length; index++)
            {
                float local = time - starts[index];
                if (local >= 0f)
                {
                    value += Wave(frequencies[index], time) * Mathf.Exp(-local * 12f) * 0.22f;
                }
            }
            return value * Mathf.Clamp01((duration - time) / 0.03f);
        }

        private static float Arpeggio(float time, float duration, int[] notes)
        {
            float step = duration / notes.Length;
            int index = Math.Min((int)(time / step), notes.Length - 1);
            float local = time - index * step;
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(local / step));
            return (Wave(Midi(notes[index]), time) + 0.25f * Wave(Midi(notes[index]) * 2f, time)) * envelope * 0.28f;
        }

        private static float EffectDuration(string cueId)
        {
            switch (cueId)
            {
                case "SFX_MINIGAME_COMPLETE": return 1.5f;
                case "SFX_PAYMENT_COINS":
                case "SFX_EXPENSE_PAY": return 0.75f;
                default: return 0.48f;
            }
        }

        private static void BindBootstrap()
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != BootstrapPath)
            {
                if (scene.isDirty)
                {
                    throw new InvalidOperationException("Save or discard current scene changes before binding Bootstrap audio.");
                }
                scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            }

            UnityAudioService service = UnityEngine.Object.FindAnyObjectByType<UnityAudioService>();
            if (service == null) throw new InvalidOperationException("Bootstrap has no UnityAudioService.");

            var serialized = new SerializedObject(service);
            SerializedProperty cues = serialized.FindProperty("cues");
            cues.arraySize = Cues.Length;
            for (int index = 0; index < Cues.Length; index++)
            {
                SerializedProperty cue = cues.GetArrayElementAtIndex(index);
                cue.FindPropertyRelative("cueId").stringValue = Cues[index].Key;
                cue.FindPropertyRelative("clip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(Cues[index].Value);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureImporters()
        {
            foreach (KeyValuePair<string, string> cue in Cues)
            {
                var importer = AssetImporter.GetAtPath(cue.Value) as AudioImporter;
                if (importer == null) continue;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                bool music = cue.Key.StartsWith("BGM_", StringComparison.Ordinal);
                settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = music ? 0.7f : 0.85f;
                settings.preloadAudioData = !music;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
        }

        private static void WriteWave(string path, float[] samples)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                foreach (float sample in samples)
                {
                    writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
                }
            }
        }

        private static void FadeEdges(float[] samples, float seconds)
        {
            int count = Math.Min(samples.Length / 2, Mathf.CeilToInt(SampleRate * seconds));
            for (int index = 0; index < count; index++)
            {
                float fade = index / (float)count;
                samples[index] *= fade;
                samples[samples.Length - 1 - index] *= fade;
            }
        }

        private static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);
        private static float Wave(float frequency, float time) => Mathf.Sin(2f * Mathf.PI * frequency * time);

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value) hash = hash * 31 + character;
                return hash;
            }
        }

        private static KeyValuePair<string, string> Pair(string cue, string path)
            => new KeyValuePair<string, string>(cue, path);
    }
}
