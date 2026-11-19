using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Witch.Application.Ports;
using Witch.Infrastructure.Audio;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class UnityAudioServicePlayModeTests
    {
        [UnityTest]
        public IEnumerator ShopMusicFadesFromSilenceToConfiguredVolumeOverFiveSeconds()
        {
            var root = new GameObject("Audio service fade test");
            root.SetActive(false);
            AudioSource music = root.AddComponent<AudioSource>();
            AudioSource sfx = root.AddComponent<AudioSource>();
            AudioSource ambience = root.AddComponent<AudioSource>();
            AudioSource ui = root.AddComponent<AudioSource>();
            UnityAudioService service = root.AddComponent<UnityAudioService>();
            AudioClip clip = AudioClip.Create("Shop test", 44100 * 6, 1, 44100, false);

            SetField(service, "music", music);
            SetField(service, "sfx", sfx);
            SetField(service, "ambience", ambience);
            SetField(service, "ui", ui);
            SetCues(service, new[]
            {
                (GameAudioCueIds.ShopMusic, clip),
                (GameAudioCueIds.ShopAmbience, clip)
            });
            root.SetActive(true);
            service.ApplySettings(new GameSettingsState(1f, 1f, 1f, false));

            Assert.That(service.PlayMusic(GameAudioCueIds.ShopMusic).Succeeded, Is.True);
            Assert.That(music.volume, Is.EqualTo(0f));

            yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(music.volume, Is.InRange(0.16f, 0.19f));

            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(music.volume, Is.EqualTo(0.35f).Within(0.001f));

            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(clip);
        }

        private static void SetCues(UnityAudioService service, (string cueId, AudioClip clip)[] entries)
        {
            Type cueType = typeof(UnityAudioService).GetNestedType("Cue", BindingFlags.NonPublic);
            Array cues = Array.CreateInstance(cueType, entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                object cue = Activator.CreateInstance(cueType);
                cueType.GetField("cueId").SetValue(cue, entries[i].cueId);
                cueType.GetField("clip").SetValue(cue, entries[i].clip);
                cues.SetValue(cue, i);
            }
            SetField(service, "cues", cues);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
