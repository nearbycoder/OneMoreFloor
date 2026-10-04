using System.Collections.Generic;

namespace OneMoreFloor.Core
{
    /// <summary>Small deterministic PRNG (SplitMix64) so a seed always replays the same shift.</summary>
    public sealed class Rng
    {
        ulong state;

        public Rng(ulong seed) { state = seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; }

        public ulong NextU64()
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Value() => (NextU64() >> 40) / (float)(1UL << 24);

        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>Uniform int in [min, max).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextU64() % (ulong)(max - min));

        public bool Chance(float p) => Value() < p;

        public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];

        /// <summary>Weighted pick; returns -1 when every weight is zero.</summary>
        public int PickWeighted(IList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += weights[i] > 0 ? weights[i] : 0;
            if (total <= 0f) return -1;
            float r = Value() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0) continue;
                r -= weights[i];
                if (r < 0f) return i;
            }
            for (int i = weights.Count - 1; i >= 0; i--) if (weights[i] > 0) return i;
            return -1;
        }
    }
}
