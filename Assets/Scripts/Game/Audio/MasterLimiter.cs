using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Brickwall-ish limiter on the final mix (attached to the AudioListener). Unity sums every
    /// source with no headroom management, so pile-ups of effects over the music would clip.
    /// Fast attack, slow release, 0.89 ceiling.
    /// </summary>
    public sealed class MasterLimiter : MonoBehaviour
    {
        public float Ceiling = 0.89f;
        float gain = 1f;
        float attack, release;

        void Awake()
        {
            float sr = AudioSettings.outputSampleRate;
            attack = 1f - Mathf.Exp(-1f / (0.0008f * sr));
            release = 1f - Mathf.Exp(-1f / (0.12f * sr));
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            for (int i = 0; i < data.Length; i += channels)
            {
                float peak = 0f;
                for (int c = 0; c < channels; c++) peak = Mathf.Max(peak, Mathf.Abs(data[i + c]));
                float want = peak * gain > Ceiling ? Ceiling / Mathf.Max(peak, 1e-6f) : 1f;
                gain += (want - gain) * (want < gain ? attack : release);
                for (int c = 0; c < channels; c++)
                {
                    float v = data[i + c] * gain;
                    // soft safety clip for anything the follower misses
                    data[i + c] = v > Ceiling ? Ceiling + (1f - Ceiling) * (float)System.Math.Tanh((v - Ceiling) / (1f - Ceiling))
                                : v < -Ceiling ? -Ceiling - (1f - Ceiling) * (float)System.Math.Tanh((-v - Ceiling) / (1f - Ceiling)) : v;
                }
            }
        }
    }
}
