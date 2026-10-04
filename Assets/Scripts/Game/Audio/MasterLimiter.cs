using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Lookahead brickwall limiter on the final mix (attached to the AudioListener). Unity sums every
    /// source with no headroom management, so pile-ups of effects over the music would clip. The output is
    /// delayed by 1.5 ms so the gain can come down smoothly *before* a peak arrives (no overshoot, no
    /// clipping distortion); it recovers over ~120 ms. Ceiling 0.85 (-1.4 dBFS) leaves room for
    /// inter-sample peaks.
    /// </summary>
    public sealed class MasterLimiter : MonoBehaviour
    {
        public float Ceiling = 0.85f;
        float gain = 1f;
        float attack, release;
        int look, pos, channels;
        float[] delay;       // look frames x channels, interleaved
        float[] required;    // gain each of the frames in the window needs

        void Awake()
        {
            float sr = AudioSettings.outputSampleRate;
            look = Mathf.Max(8, Mathf.RoundToInt(0.0015f * sr));
            attack = 1f - Mathf.Exp(-4f / look);   // ~98% of the way within the lookahead
            release = 1f - Mathf.Exp(-1f / (0.12f * sr));
            required = new float[look];
            for (int i = 0; i < look; i++) required[i] = 1f;
        }

        void OnAudioFilterRead(float[] data, int ch)
        {
            if (delay == null || channels != ch)
            {
                channels = ch;
                delay = new float[look * ch];
                pos = 0;
            }
            for (int i = 0; i < data.Length; i += ch)
            {
                float peak = 0f;
                for (int c = 0; c < ch; c++) peak = Mathf.Max(peak, Mathf.Abs(data[i + c]));
                required[pos] = peak > Ceiling ? Ceiling / peak : 1f;
                float need = 1f;
                for (int k = 0; k < look; k++) if (required[k] < need) need = required[k];
                gain += (need - gain) * (need < gain ? attack : release);
                int d = pos * ch;
                for (int c = 0; c < ch; c++)
                {
                    float outv = delay[d + c] * gain;
                    delay[d + c] = data[i + c];
                    // the window guarantees this almost never triggers; it's a last line of defence
                    data[i + c] = Mathf.Clamp(outv, -Ceiling, Ceiling);
                }
                pos = (pos + 1) % look;
            }
        }
    }
}
