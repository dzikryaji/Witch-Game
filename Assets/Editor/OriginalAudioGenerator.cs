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
            Pair("BGM_MAIN_MENU", BgmFolder + "/MainMenu_Original.wav"),
            Pair("BGM_SHOP", BgmFolder + "/Shop_Original.wav"),
            Pair("BGM_REWIND_MINIGAME", BgmFolder + "/Rewind_Original.wav"),
            Pair("BGM_GAME_OVER", BgmFolder + "/GameOver_Original.wav"),
            Pair("BGM_GAME_COMPLETE", BgmFolder + "/Victory_MedievalTrumpet_Original.wav"),
            Pair("SFX_SHOP_AMBIENCE", SfxFolder + "/ShopAmbience_Original.wav"),
            Pair("SFX_CUSTOMER_BELL", SfxFolder + "/CustomerBell_Original.wav"),
            Pair("SFX_GUIDEBOOK_OPEN", SfxFolder + "/GuidebookOpen_Original.wav"),
            Pair("SFX_GUIDEBOOK_CLOSE", SfxFolder + "/GuidebookClose_Original.wav"),
            Pair("SFX_GRINDER_COMPLETE", SfxFolder + "/Grinder_Original.wav"),
            Pair("SFX_POT_ADD_HERB", SfxFolder + "/PotAdd_Original.wav"),
            Pair("SFX_BREW_SUCCESS", SfxFolder + "/BrewSuccess_Original.wav"),
            Pair("SFX_BREW_INVALID", SfxFolder + "/BrewInvalid_Original.wav"),
            Pair("SFX_DISCARD", SfxFolder + "/Discard_Original.wav"),
            Pair("SFX_ORDER_CORRECT", SfxFolder + "/OrderCorrect_Original.wav"),
            Pair("SFX_PAYMENT_COINS", SfxFolder + "/Coins_Original.wav"),
            Pair("SFX_ORDER_WRONG", SfxFolder + "/OrderWrong_Original.wav"),
            Pair("SFX_HUNTER_WARNING", SfxFolder + "/HunterKnock_Original.wav"),
            Pair("SFX_TIME_STOP", SfxFolder + "/TimeStop_Original.wav"),
            Pair("SFX_CARD_FLIP", SfxFolder + "/CardFlip_Original.wav"),
            Pair("SFX_CARD_MATCH", SfxFolder + "/CardMatch_Original.wav"),
            Pair("SFX_CARD_MISMATCH", SfxFolder + "/CardMismatch_Original.wav"),
            Pair("SFX_MINIGAME_COMPLETE", SfxFolder + "/MinigameComplete_Original.wav"),
            Pair("SFX_EXPENSE_PAY", SfxFolder + "/ExpensePay_Original.wav"),
            Pair("SFX_EXPENSE_FAIL", SfxFolder + "/ExpenseFail_Original.wav"),
            Pair("SFX_UI_CONFIRM", SfxFolder + "/UiConfirm_Original.wav"),
            Pair("SFX_UI_BACK", SfxFolder + "/UiBack_Original.wav")
        };

        [MenuItem("Witch/Audio/Generate Original Audio and Bind Bootstrap")]
        public static void GenerateAndBind()
        {
            Directory.CreateDirectory(BgmFolder);
            Directory.CreateDirectory(SfxFolder);

            foreach (KeyValuePair<string, string> cue in Cues)
            {
                float[] samples = cue.Key.StartsWith("BGM_", StringComparison.Ordinal)
                    ? CreateMusic(cue.Key)
                    : CreateEffect(cue.Key);
                WriteWave(cue.Value, samples);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporters();
            BindBootstrap();
            Debug.Log("Generated and bound " + Cues.Length + " original audio clips.");
        }

        private static float[] CreateMusic(string cueId)
        {
            if (cueId == "BGM_GAME_COMPLETE")
            {
                return CreateFanfare();
            }

            int seconds = cueId == "BGM_GAME_OVER" ? 6 : 16;
            var result = new float[SampleRate * seconds];
            int[] notes;
            float step;
            if (cueId == "BGM_MAIN_MENU")
            {
                notes = new[] { 50, 57, 62, 65, 62, 57, 48, 55, 60, 64, 60, 55 };
                step = 0.5f;
            }
            else if (cueId == "BGM_SHOP")
            {
                notes = new[] { 45, 52, 57, 60, 57, 52, 47, 54, 59, 62, 59, 54 };
                step = 0.375f;
            }
            else if (cueId == "BGM_REWIND_MINIGAME")
            {
                notes = new[] { 69, 72, 76, 81, 76, 72, 67, 71, 74, 79, 74, 71 };
                step = 0.25f;
            }
            else
            {
                notes = new[] { 50, 48, 45, 43, 41, 38 };
                step = 0.85f;
            }

            for (int index = 0; index < result.Length; index++)
            {
                float time = index / (float)SampleRate;
                int noteIndex = Math.Min((int)(time / step), notes.Length - 1);
                if (cueId != "BGM_GAME_OVER") noteIndex %= notes.Length;
                float local = time - Mathf.Floor(time / step) * step;
                float frequency = Midi(notes[noteIndex]);
                float envelope = Mathf.Exp(-local * (cueId == "BGM_REWIND_MINIGAME" ? 5.5f : 3.2f));
                float pluck = Wave(frequency, time) + 0.35f * Wave(frequency * 2f, time);
                float drone = 0.18f * Wave(Midi(notes[0] - 12), time);
                float pulse = cueId == "BGM_REWIND_MINIGAME"
                    ? 0.12f * Wave(2f, time) * Wave(880f, time)
                    : 0f;
                result[index] = (pluck * envelope * 0.22f) + (drone * 0.18f) + pulse;
            }

            FadeEdges(result, 0.05f);
            return result;
        }

        private static float[] CreateFanfare()
        {
            const int seconds = 8;
            var result = new float[SampleRate * seconds];
            int[] melody = { 55, 60, 64, 67, 64, 67, 72, 71, 72 };
            float[] starts = { 0f, 0.55f, 1.1f, 1.65f, 2.35f, 2.8f, 3.25f, 4.3f, 5.15f };
            float[] lengths = { 0.5f, 0.5f, 0.5f, 0.65f, 0.4f, 0.4f, 0.95f, 0.65f, 2.1f };
            for (int index = 0; index < result.Length; index++)
            {
                float time = index / (float)SampleRate;
                float value = 0f;
                for (int note = 0; note < melody.Length; note++)
                {
                    float local = time - starts[note];
                    if (local < 0f || local > lengths[note]) continue;
                    float attack = Mathf.Clamp01(local / 0.04f);
                    float release = Mathf.Clamp01((lengths[note] - local) / 0.18f);
                    float frequency = Midi(melody[note]);
                    float brass = Wave(frequency, time)
                        + 0.5f * Wave(frequency * 2f, time)
                        + 0.22f * Wave(frequency * 3f, time);
                    value += brass * attack * release * 0.18f;
                }
                result[index] = value;
            }
            FadeEdges(result, 0.02f);
            return result;
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
                    case "SFX_SHOP_AMBIENCE":
                        value = noise * 0.018f + Wave(55f, time) * 0.018f
                            + Wave(0.23f, time) * Wave(220f, time) * 0.012f;
                        break;
                    case "SFX_CUSTOMER_BELL":
                        value = (Wave(1046.5f, time) + 0.55f * Wave(1569.8f, time)) * Mathf.Exp(-time * 3.8f) * 0.32f;
                        break;
                    case "SFX_HUNTER_WARNING":
                        float knockPhase = time % 0.48f;
                        value = knockPhase < 0.11f ? (noise * 0.42f + Wave(92f, time) * 0.5f) * Mathf.Exp(-knockPhase * 24f) : 0f;
                        break;
                    case "SFX_TIME_STOP":
                        value = (noise * 0.15f + Wave(1200f - 1050f * progress, time) * 0.3f) * (1f - progress);
                        break;
                    case "SFX_GRINDER_COMPLETE":
                        value = (noise * 0.3f + Wave(90f + 20f * Wave(7f, time), time) * 0.25f) * Mathf.Sin(Mathf.PI * progress);
                        break;
                    case "SFX_GUIDEBOOK_OPEN":
                    case "SFX_GUIDEBOOK_CLOSE":
                    case "SFX_CARD_FLIP":
                    case "SFX_DISCARD":
                        value = noise * Mathf.Sin(Mathf.PI * progress) * 0.22f + Wave(260f + 220f * progress, time) * (1f - progress) * 0.08f;
                        break;
                    case "SFX_PAYMENT_COINS":
                    case "SFX_EXPENSE_PAY":
                        value = CoinCluster(time, duration);
                        break;
                    case "SFX_POT_ADD_HERB":
                        value = Wave(180f + 520f * progress * progress, time) * Mathf.Sin(Mathf.PI * progress) * 0.3f;
                        break;
                    case "SFX_BREW_SUCCESS":
                    case "SFX_ORDER_CORRECT":
                    case "SFX_CARD_MATCH":
                    case "SFX_MINIGAME_COMPLETE":
                    case "SFX_UI_CONFIRM":
                        value = Arpeggio(time, duration, new[] { 60, 64, 67, 72 });
                        break;
                    case "SFX_BREW_INVALID":
                    case "SFX_ORDER_WRONG":
                    case "SFX_CARD_MISMATCH":
                    case "SFX_EXPENSE_FAIL":
                    case "SFX_UI_BACK":
                        value = Arpeggio(time, duration, new[] { 55, 51, 48, 43 });
                        break;
                    default:
                        value = Wave(440f, time) * (1f - progress) * 0.2f;
                        break;
                }
                result[index] = value;
            }
            FadeEdges(result, cueId == "SFX_SHOP_AMBIENCE" ? 0.08f : 0.005f);
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
                case "SFX_SHOP_AMBIENCE": return 12f;
                case "SFX_HUNTER_WARNING": return 1.55f;
                case "SFX_TIME_STOP": return 1.2f;
                case "SFX_GRINDER_COMPLETE": return 0.9f;
                case "SFX_CUSTOMER_BELL": return 1.1f;
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
