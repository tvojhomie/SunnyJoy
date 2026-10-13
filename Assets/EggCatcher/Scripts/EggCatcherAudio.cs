using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EggCatcher
{
    public enum EggSound
    {
        Click, Tick, Go, Catch, Corn, Worm, Heart, Magnet, Hurt, Bomb, Lose, Record,
        SkinSelect, BackgroundSelect, Purchase, Error, Toggle, Confirm, Back, Pause, Resume,
        Combo, StreakLost, MagnetEnd, Shield, ShieldBlock, Frenzy, FrenzyEnd, Mission
    }

    [DisallowMultipleComponent]
    public sealed class EggCatcherAudio : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private AudioSource music, effects, uiEffects, resultEffects;
        private readonly Dictionary<EggSound, AudioClip> clips = new Dictionary<EggSound, AudioClip>();
        private AudioClip musicClip;
        private bool soundsEnabled = true;
        private float lastCatch = -1;
        private int lastUiFrame = -1;
        private Coroutine resultRoutine;
        public event Action<EggSound> SoundPlayed;
        public bool MusicEnabled => music != null && !music.mute;
        public bool SoundsEnabled => soundsEnabled;

        public void Initialize()
        {
            if (music != null) return;
            music = gameObject.AddComponent<AudioSource>();
            effects = gameObject.AddComponent<AudioSource>();
            uiEffects = gameObject.AddComponent<AudioSource>();
            resultEffects = gameObject.AddComponent<AudioSource>();
            foreach (var source in new[] { music, effects, uiEffects, resultEffects })
            {
                source.playOnAwake = false;
                source.spatialBlend = 0;
            }
            music.loop = true;
            music.volume = .22f;
            effects.volume = .65f;
            uiEffects.volume = .55f;
            resultEffects.volume = .75f;
            resultEffects.priority = 32;
            musicClip = MakeMusic();
            music.clip = musicClip;
            foreach (EggSound sound in Enum.GetValues(typeof(EggSound))) clips[sound] = MakeEffect(sound);
        }

        public void ApplySettings(bool musicEnabled, bool soundEnabled)
        {
            Initialize();
            music.mute = !musicEnabled;
            soundsEnabled = soundEnabled;
            if (!soundEnabled)
            {
                effects.Stop();
                uiEffects.Stop();
                CancelResult();
            }
            if (musicEnabled && !music.isPlaying) music.Play();
        }

        public void Duck(bool paused) { if (music != null) music.volume = paused ? .09f : .22f; }

        public void Play(EggSound sound)
        {
            if (!soundsEnabled || effects == null) return;

            if (sound == EggSound.Catch)
            {
                if (Time.unscaledTime - lastCatch < .065f) return;
                lastCatch = Time.unscaledTime;
            }
            Emit(effects, sound);
        }

        public void PlayUi(EggSound sound = EggSound.Click)
        {
            if (sound == EggSound.Click && lastUiFrame == Time.frameCount) return;
            lastUiFrame = Time.frameCount;
            if (!soundsEnabled || uiEffects == null) return;
            uiEffects.Stop();
            Emit(uiEffects, sound);
        }

        public void PlayResult(bool newRecord)
        {
            CancelResult();
            if (soundsEnabled) resultRoutine = StartCoroutine(ResultSequence(newRecord));
        }

        private IEnumerator ResultSequence(bool newRecord)
        {

            yield return new WaitForSecondsRealtime(.22f);
            Emit(resultEffects, EggSound.Lose);
            if (newRecord)
            {
                yield return new WaitForSecondsRealtime(clips[EggSound.Lose].length + .12f);
                Emit(resultEffects, EggSound.Record);
            }
            resultRoutine = null;
        }

        public void CancelResult()
        {
            if (resultRoutine != null) StopCoroutine(resultRoutine);
            resultRoutine = null;
            if (resultEffects != null) resultEffects.Stop();
            lastCatch = -1;
        }

        private void Emit(AudioSource source, EggSound sound)
        {
            if (!soundsEnabled) return;
            source.PlayOneShot(clips[sound]);
            SoundPlayed?.Invoke(sound);
        }

        private static AudioClip MakeEffect(EggSound sound)
        {
            double[] notes;
            float step = .08f;
            switch (sound)
            {
                case EggSound.Click: notes = new[] { 880.0, 1320.0 }; step = .028f; break;
                case EggSound.Tick: notes = new[] { 440.0 }; step = .09f; break;
                case EggSound.Go: notes = new[] { 523.25, 659.25, 783.99 }; break;
                case EggSound.Corn: notes = new[] { 783.99, 987.77 }; break;
                case EggSound.Worm: notes = new[] { 880.0, 1108.73, 1318.51 }; step = .055f; break;
                case EggSound.Heart: notes = new[] { 523.25, 659.25, 783.99, 1046.5 }; break;
                case EggSound.Magnet: notes = new[] { 392.0, 523.25, 659.25, 783.99, 1046.5 }; step = .06f; break;
                case EggSound.Hurt: notes = new[] { 220.0, 146.83 }; step = .12f; break;
                case EggSound.Bomb: notes = new[] { 98.0, 65.41 }; step = .16f; break;
                case EggSound.Lose: notes = new[] { 392.0, 369.99, 329.63, 196.0 }; step = .19f; break;
                case EggSound.Record: notes = new[] { 523.25, 659.25, 783.99, 1046.5, 783.99, 1046.5 }; step = .15f; break;
                case EggSound.SkinSelect: notes = new[] { 659.25, 880.0, 1318.51 }; step = .065f; break;
                case EggSound.BackgroundSelect: notes = new[] { 392.0, 587.33, 783.99, 1174.66 }; step = .07f; break;
                case EggSound.Purchase: notes = new[] { 1046.5, 1318.51, 1567.98, 2093.0 }; step = .075f; break;
                case EggSound.Error: notes = new[] { 196.0, 185.0 }; step = .13f; break;
                case EggSound.Toggle: notes = new[] { 740.0, 987.77 }; step = .035f; break;
                case EggSound.Confirm: notes = new[] { 523.25, 783.99, 1046.5 }; step = .09f; break;
                case EggSound.Back: notes = new[] { 880.0, 659.25 }; step = .045f; break;
                case EggSound.Pause: notes = new[] { 659.25, 523.25 }; step = .08f; break;
                case EggSound.Resume: notes = new[] { 523.25, 659.25 }; step = .08f; break;
                case EggSound.Combo: notes = new[] { 1318.51, 1567.98, 2093.0 }; step = .08f; break;
                case EggSound.StreakLost: notes = new[] { 440.0, 293.66 }; step = .055f; break;
                case EggSound.MagnetEnd: notes = new[] { 783.99, 587.33, 392.0 }; step = .055f; break;
                case EggSound.Shield: notes = new[] { 659.25, 987.77, 1318.51 }; step = .075f; break;
                case EggSound.ShieldBlock: notes = new[] { 392.0, 783.99, 1174.66 }; step = .045f; break;
                case EggSound.Frenzy: notes = new[] { 523.25, 783.99, 1046.5, 1567.98 }; step = .11f; break;
                case EggSound.FrenzyEnd: notes = new[] { 1046.5, 783.99, 523.25 }; step = .07f; break;
                case EggSound.Mission: notes = new[] { 659.25, 783.99, 987.77, 1318.51 }; step = .09f; break;
                default: notes = new[] { 1046.5, 1318.51 }; step = .055f; break;
            }
            bool rough = sound == EggSound.Bomb || sound == EggSound.Hurt || sound == EggSound.Error;
            var samples = new float[(int)((notes.Length * step + .25f) * SampleRate)];
            for (int i = 0; i < notes.Length; i++)
            {
                AddNote(samples, i * step, step + .19f, notes[i], .4f, rough, 4.2);
                if (sound == EggSound.Record || sound == EggSound.BackgroundSelect)
                    AddNote(samples, i * step, step + .21f, notes[i] * 1.5, .13f, false, 4.2);
            }
            if (sound == EggSound.Bomb) AddImpact(samples);
            float peak = .001f;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            float level = sound == EggSound.StreakLost || sound == EggSound.MagnetEnd ? .28f : .7f;
            for (int i = 0; i < samples.Length; i++) samples[i] *= level / peak;
            return Clip("EggCatcher_" + sound, samples);
        }

        private static void AddImpact(float[] samples)
        {
            var noise = new System.Random(179);
            double phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / SampleRate;
                phase += 2 * Math.PI * (48 + 130 * Math.Exp(-t * 16)) / SampleRate;
                double envelope = Math.Min(1, t / .004) * Math.Exp(-t * 12);
                samples[i] += (float)((Math.Sin(phase) * .65 + (noise.NextDouble() * 2 - 1) * .24) * envelope);
            }
        }

        private static AudioClip MakeMusic()
        {

            const float beat = 60f / 108;
            var samples = new float[(int)(32 * beat * SampleRate)];
            int[] melody = { 76, 79, 81, 79, 76, 74, 72, 0, 74, 77, 79, 77, 74, 72, 71, 0,
                72, 76, 79, 81, 79, 76, 74, 72, 71, 74, 77, 79, 76, 74, 72, 0 };
            int[] bass = { 48, 53, 55, 48, 57, 53, 55, 48 };
            for (int i = 0; i < 32; i++)
            {
                if (melody[i] != 0) AddNote(samples, i * beat, .42f, Midi(melody[i]), .15f, false);
                AddNote(samples, i * beat, .25f, Midi(bass[i / 4]), .16f, false);
                if (i % 2 == 1) AddNote(samples, i * beat + beat * .5f, .07f, 1800, .025f, true);
            }
            return Clip("EggCatcher_MeadowLoop", samples);
        }

        private static double Midi(int note) => 440 * Math.Pow(2, (note - 69) / 12.0);
        private static void AddNote(float[] data, float start, float duration, double frequency, float gain, bool rough, double decay = 9)
        {
            int offset = (int)(start * SampleRate), length = (int)(duration * SampleRate);
            for (int i = 0; i < length && offset + i < data.Length; i++)
            {
                double t = (double)i / SampleRate, phase = 2 * Math.PI * frequency * t;
                double envelope = Math.Min(1, t / .008) * Math.Exp(-t * decay / duration) * Math.Min(1, (duration - t) / .012);
                double tone = Math.Sin(phase) + .24 * Math.Sin(phase * 2) + .09 * Math.Sin(phase * 3);
                if (rough) tone += .3 * Math.Sin(phase * 1.47) * Math.Sin(phase * 5.1);
                data[offset + i] += (float)(tone * envelope * gain);
            }
        }
        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        private void OnDestroy()
        {
            if (musicClip != null) Destroy(musicClip);
            foreach (var clip in clips.Values) Destroy(clip);
        }
    }
}
