using System.Collections.Generic;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Music that responds to trouble, plus pooled sound effects. The gameplay track is five stems
    /// started on the same DSP tick; they share one pitch so they never drift apart.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        public float MasterVolume = 1f, MusicVolume = 0.8f, SfxVolume = 0.9f;
        // gain staging: every clip is mastered near full scale, so the sums need headroom
        const float MusicGain = 0.42f, SfxGain = 0.6f;

        enum Stem { Bed, Melody, Trouble, Rush, Night }
        static readonly string[] StemNames = { "going_up_bed", "going_up_melody", "going_up_trouble", "going_up_rush", "going_up_night" };

        readonly AudioSource[] stems = new AudioSource[5];
        readonly AudioLowPassFilter[] stemLp = new AudioLowPassFilter[5];
        readonly float[] stemVol = new float[5];
        AudioSource title, sting, motor, ambOcean, ambCity;
        AudioLowPassFilter titleLp;
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        bool gameplay;
        float gameplayMix, titleMix;
        float duck = 1f, duckTarget = 1f;
        float muffle;
        float trouble, rush, night, calmMelody = 1f;
        float motorLevel;

        public static AudioDirector Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioDirector>();
            go.AddComponent<AudioListener>();
            go.AddComponent<MasterLimiter>();
            AudioTap.TryStart(go);
            a.Build();
            Instance = a;
            return a;
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void Build()
        {
            for (int i = 0; i < stems.Length; i++)
            {
                stems[i] = NewSource("Stem_" + StemNames[i], true);
                stems[i].clip = Clip("Music/" + StemNames[i]);
                stems[i].volume = 0f;
                stemLp[i] = stems[i].gameObject.AddComponent<AudioLowPassFilter>();
                stemLp[i].cutoffFrequency = 22000f;
            }
            title = NewSource("Title", true);
            title.clip = Clip("Music/lobby_lounge");
            titleLp = title.gameObject.AddComponent<AudioLowPassFilter>();
            titleLp.cutoffFrequency = 22000f;
            sting = NewSource("Sting", false);
            motor = NewSource("Motor", true);
            motor.clip = Clip("Sfx/motor_loop");
            motor.volume = 0f;
            ambOcean = NewSource("Ocean", true);
            ambOcean.clip = Clip("Sfx/amb_ocean");
            ambCity = NewSource("City", true);
            ambCity.clip = Clip("Sfx/amb_city");
            for (int i = 0; i < 20; i++) pool.Add(NewSource("Sfx" + i, false));
        }

        public AudioClip Clip(string path)
        {
            if (clips.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + path);
            if (c == null) Debug.LogWarning("[Audio] missing clip " + path);
            clips[path] = c;
            return c;
        }

        // ---------------------------------------------------------------- music

        public void PlayTitle()
        {
            gameplay = false;
            if (!title.isPlaying) { title.volume = 0f; title.Play(); }
            if (!ambCity.isPlaying) { ambCity.volume = 0f; ambCity.Play(); }
        }

        public void PlayGameplay(ShiftDef def)
        {
            gameplay = true;
            night = def.Music == "night" ? 1f : 0f;
            if (!stems[0].isPlaying)
            {
                double at = AudioSettings.dspTime + 0.12;
                foreach (var s in stems) { s.volume = 0f; s.PlayScheduled(at); }
            }
            if (!ambCity.isPlaying) { ambCity.volume = 0f; ambCity.Play(); }
            if (!motor.isPlaying) motor.Play();
        }

        public void StopGameplayMusic()
        {
            gameplay = false;
        }

        /// <summary>Called every frame with the game state while a shift runs.</summary>
        public void UpdateShift(ShiftSim sim, float dt, bool paused)
        {
            float t = sim.Trouble;
            trouble = Mathf.Lerp(trouble, t, Ease.Damp(t > trouble ? 2.5f : 0.8f, dt));
            bool hot = sim.RushHour || sim.Streak >= 18;
            rush = Mathf.MoveTowards(rush, hot ? 1f : 0f, dt * 0.5f);
            bool spooky = sim.Def.Music == "night" || sim.Car.Has(Kind.Vampire);
            night = Mathf.MoveTowards(night, spooky ? 1f : 0f, dt * 0.4f);
            calmMelody = Mathf.MoveTowards(calmMelody, trouble > 0.55f ? 0.35f : 1f, dt * 0.5f);
            muffle = Mathf.MoveTowards(muffle, paused || sim.Ended ? 1f : 0f, dt * 3f);

            float speed = Mathf.Abs(sim.Car.Vel) / Tuning.MaxSpeed;
            motorLevel = Mathf.Lerp(motorLevel, speed, Ease.Damp(10f, dt));
            motor.volume = motorLevel * 0.35f * SfxVolume * MasterVolume * SfxGain * (paused ? 0f : 1f);
            motor.pitch = 0.8f + motorLevel * 0.5f;

            bool ocean = sim.B.Has(FloorId.Ocean);
            ambOcean.volume = Mathf.MoveTowards(ambOcean.volume, ocean && !paused ? 0.35f * SfxVolume * MasterVolume : 0f, dt);
            if (ocean && !ambOcean.isPlaying) ambOcean.Play();
        }

        void Update()
        {
            float dt = UiTime.Dt;
            gameplayMix = Mathf.MoveTowards(gameplayMix, gameplay ? 1f : 0f, dt * 0.8f);
            titleMix = Mathf.MoveTowards(titleMix, gameplay ? 0f : 1f, dt * 0.6f);
            duck = Mathf.MoveTowards(duck, duckTarget, dt * (duckTarget < duck ? 6f : 1.2f));
            if (!sting.isPlaying) duckTarget = 1f;

            float music = MusicVolume * MasterVolume * duck * MusicGain;
            stemVol[(int)Stem.Bed] = 0.9f;
            stemVol[(int)Stem.Melody] = 0.85f * calmMelody;
            stemVol[(int)Stem.Trouble] = Mathf.SmoothStep(0f, 0.95f, (trouble - 0.15f) / 0.6f);
            stemVol[(int)Stem.Rush] = 0.8f * rush;
            stemVol[(int)Stem.Night] = 0.8f * night;
            // tape warble when things go wrong; every stem shares the pitch so they stay locked
            float wobble = 1f + Mathf.Sin(UiTime.Now * 2f * Mathf.PI * 0.55f) * 0.011f * Mathf.Max(0f, trouble - 0.45f) * 2f;
            float cutoff = Mathf.Lerp(22000f, 900f, Mathf.Max(muffle, Mathf.Max(0f, trouble - 0.85f) * 3f));
            for (int i = 0; i < stems.Length; i++)
            {
                stems[i].volume = stemVol[i] * gameplayMix * music;
                stems[i].pitch = wobble;
                stemLp[i].cutoffFrequency = cutoff;
            }
            if (gameplayMix <= 0f && !gameplay && stems[0].isPlaying) foreach (var s in stems) s.Stop();
            title.volume = titleMix * music * 0.85f;
            titleLp.cutoffFrequency = Mathf.Lerp(22000f, 1200f, muffleTitle);
            if (titleMix <= 0f && title.isPlaying && gameplay) title.Stop();
            ambCity.volume = 0.25f * SfxVolume * MasterVolume * SfxGain;
            if (!gameplay)
            {
                motor.volume = Mathf.MoveTowards(motor.volume, 0f, dt);
                ambOcean.volume = Mathf.MoveTowards(ambOcean.volume, 0f, dt);
            }
        }

        float muffleTitle;
        public void MuffleTitle(bool on) => muffleTitle = on ? 1f : 0f;

        public void Sting(string name, float duckTo = 0.35f, float volume = 1f)
        {
            var c = Clip("Music/" + name);
            if (c == null) return;
            sting.Stop();
            sting.clip = c;
            sting.volume = volume * MusicVolume * MasterVolume * MusicGain * 1.4f;
            sting.Play();
            duckTarget = duckTo;
        }

        // ---------------------------------------------------------------- effects

        /// <summary>Plays an effect. pan: -1 left .. 1 right (use ScreenPan for world positions).</summary>
        public void Sfx(string name, float volume = 1f, float pitch = 1f, float pan = 0f, float pitchJitter = 0.04f, float minGap = 0.03f)
        {
            if (lastPlayed.TryGetValue(name, out var last) && UiTime.Now - last < minGap) return;
            lastPlayed[name] = UiTime.Now;
            var c = Clip("Sfx/" + name);
            if (c == null) return;
            AudioSource src = null;
            foreach (var s in pool) if (!s.isPlaying) { src = s; break; }
            if (src == null) src = pool[0];
            src.clip = c;
            src.volume = volume * SfxVolume * MasterVolume * SfxGain;
            src.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            src.panStereo = Mathf.Clamp(pan, -1f, 1f) * 0.6f;
            src.Play();
        }

        /// <summary>Self-test: when an effect last started (UiTime), or -1 if it never has.</summary>
        public float LastPlayedAt(string name) => lastPlayed.TryGetValue(name, out var t) ? t : -1f;

        struct Pending { public float At; public string Name; public float Vol, Pitch, Pan; }
        readonly List<Pending> pending = new List<Pending>();

        public void SfxLater(string name, float delay, float volume = 1f, float pitch = 1f, float pan = 0f)
            => pending.Add(new Pending { At = UiTime.Now + delay, Name = name, Vol = volume, Pitch = pitch, Pan = pan });

        void LateUpdate()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].At > UiTime.Now) continue;
                var p = pending[i];
                pending.RemoveAt(i);
                Sfx(p.Name, p.Vol, p.Pitch, p.Pan, 0.03f, 0f);
            }
        }

        public void Voice(Kind kind, float pan = 0f, float volume = 0.55f)
        {
            Sfx($"voice_{kind.ToString().ToLowerInvariant()}_{Random.Range(0, 5)}", volume, 1f, pan, 0.06f, 0.12f);
        }

        public void Bellhop(float pan = 0f) => Sfx($"voice_bellhop_{Random.Range(0, 5)}", 0.45f, 1f, pan, 0.05f, 0.4f);

        public static float ScreenPan(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            var v = cam.WorldToViewportPoint(world);
            return (v.x - 0.5f) * 2f;
        }
    }
}
