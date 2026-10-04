using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Timescale-independent time for UI, camera and audio. Same as Time.unscaled* during normal play,
    /// but follows Time.captureDeltaTime when frames are being recorded offline, where Unity's unscaled
    /// clock keeps running at wall-clock speed.
    /// </summary>
    public static class UiTime
    {
        static int frame = -1;
        static float now;

        public static float Dt => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        public static float Now
        {
            get
            {
                int f = Time.frameCount;
                if (Time.captureDeltaTime <= 0f) now = Time.unscaledTime;
                else if (f != frame) now += Time.captureDeltaTime * Mathf.Max(1, f - frame);
                frame = f;
                return now;
            }
        }

        /// <summary>Coroutine wait in UI time (WaitForSecondsRealtime would desync recordings).</summary>
        public static System.Collections.IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) yield return null;
        }
    }
}
