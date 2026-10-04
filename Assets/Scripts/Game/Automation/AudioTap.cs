using System;
using System.IO;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Records the final mixed output (what the player hears) to a WAV so the mix can be measured
    /// without listening: launch with -omfRecordAudio &lt;file.wav&gt; [-omfRecordSeconds N].
    /// </summary>
    public sealed class AudioTap : MonoBehaviour
    {
        float[] buffer;
        int written, channels = 2, rate;
        string path;
        bool done;

        public static AudioTap Instance { get; private set; }
        /// <summary>Seconds of audio captured so far (read from the main thread; approximate to one DSP buffer).</summary>
        public double SecondsWritten => written / (double)channels / rate;
        /// <summary>Stop recording and write the file now.</summary>
        public void Finish() { if (path != null) { Write(); path = null; } }

        public static void TryStart(GameObject listener)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-omfRecordAudio");
            if (i < 0 || i + 1 >= args.Length) return;
            float seconds = 90f;
            int s = Array.IndexOf(args, "-omfRecordSeconds");
            if (s >= 0 && s + 1 < args.Length) float.TryParse(args[s + 1], out seconds);
            var tap = listener.AddComponent<AudioTap>();
            Instance = tap;
            tap.path = args[i + 1];
            tap.rate = AudioSettings.outputSampleRate;
            tap.buffer = new float[(int)(seconds * tap.rate * 2)];
        }

        void OnAudioFilterRead(float[] data, int ch)
        {
            if (buffer == null || done) return;
            channels = ch;
            int n = Math.Min(data.Length, buffer.Length - written);
            Array.Copy(data, 0, buffer, written, n);
            written += n;
            if (written >= buffer.Length) done = true;
        }

        void Update()
        {
            if (!done || path == null) return;
            Write();
            path = null;
        }

        void OnApplicationQuit() { if (path != null) Write(); }

        void Write()
        {
            done = true;
            int count = Math.Min(written, buffer.Length);
            written = count;
            var bytes = new byte[44 + count * 2];
            using (var ms = new MemoryStream(bytes))
            using (var w = new BinaryWriter(ms))
            {
                w.Write("RIFF".ToCharArray()); w.Write(36 + written * 2); w.Write("WAVE".ToCharArray());
                w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(rate);
                w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
                w.Write("data".ToCharArray()); w.Write(written * 2);
                for (int i = 0; i < count; i++) w.Write((short)Mathf.Clamp(buffer[i] * 32767f, -32768f, 32767f));
            }
            File.WriteAllBytes(path, bytes);
            Debug.Log($"[AudioTap] wrote {written / channels / (float)rate:0.0}s to {path}");
        }
    }
}
