using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace OneMoreFloor
{
    /// <summary>
    /// Plays a character's Blender-authored clips (Idle, Walk, Stomp, Tap, Cheer, Tuck, Fume, ...) through one
    /// mixer whose weights the owner sets every frame from what the character is doing. Secondary motion
    /// (cape, leaves, head shakes) is layered on top in the owner's LateUpdate with <see cref="Turn"/>.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        static readonly Dictionary<string, AnimationClip[]> clipCache = new Dictionary<string, AnimationClip[]>();

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] players;
        readonly Dictionary<string, int> index = new Dictionary<string, int>();
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        float[] weights;

        /// <summary>Hook up the clips of Resources/Models/&lt;asset&gt; to an instantiated model. Null when it has none.</summary>
        /// <summary>-omfNoRig leaves characters in their rest pose (for performance A/B runs).</summary>
        static readonly bool disabled = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-omfNoRig") >= 0;

        public static CharacterRig Attach(GameObject model, string asset, float phase)
        {
            if (disabled) return null;
            if (!clipCache.TryGetValue(asset, out var clips))
            {
                var all = Resources.LoadAll<AnimationClip>("Models/" + asset);
                var list = new List<AnimationClip>();
                foreach (var c in all) if (!c.name.StartsWith("__preview__")) list.Add(c);
                clipCache[asset] = clips = list.ToArray();
            }
            if (clips.Length == 0) return null;
            var rig = model.AddComponent<CharacterRig>();
            rig.Build(clips, phase);
            return rig;
        }

        void Build(AnimationClip[] clips, float phase)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) bones.TryAdd(t.name, t);
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = false;
            var animator = GetComponent<Animator>();
            if (!animator) animator = gameObject.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            graph = PlayableGraph.Create(name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            players = new AnimationClipPlayable[clips.Length];
            weights = new float[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                index[clips[i].name] = i;
                var p = AnimationClipPlayable.Create(graph, clips[i]);
                p.SetTime(phase * clips[i].length);   // crowds don't breathe in unison
                p.SetApplyFootIK(false);
                graph.Connect(p, 0, mixer, i);
                players[i] = p;
            }
            var output = AnimationPlayableOutput.Create(graph, "pose", animator);
            output.SetSourcePlayable(mixer);
            Set("Idle", 1f);
            Apply();
            graph.Play();
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        public Transform Bone(string boneName) => bones.TryGetValue(boneName, out var t) ? t : null;

        public bool Has(string clip) => index.ContainsKey(clip);

        /// <summary>Stage a clip's weight; call <see cref="Apply"/> once all are set (they're normalized).</summary>
        public void Set(string clip, float weight)
        {
            if (index.TryGetValue(clip, out int i)) weights[i] = Mathf.Max(0f, weight);
        }

        public void Speed(string clip, float speed)
        {
            if (index.TryGetValue(clip, out int i)) players[i].SetSpeed(speed);
        }

        /// <summary>Restart a clip from the top (for one-shot-ish loops like a cheer).</summary>
        public void Restart(string clip)
        {
            if (index.TryGetValue(clip, out int i)) players[i].SetTime(0);
        }

        public void Apply()
        {
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++) sum += weights[i];
            if (sum < 1e-4f && index.TryGetValue("Idle", out int idle)) { weights[idle] = 1f; sum = 1f; }
            for (int i = 0; i < weights.Length; i++)
            {
                mixer.SetInputWeight(i, weights[i] / sum);
                weights[i] = 0f;
            }
        }

        /// <summary>
        /// Rotate an (already animated) bone about its own pivot by a rotation expressed in the model's space
        /// (x = the character's side, y up, z = facing). Use from LateUpdate.
        /// </summary>
        public void Turn(Transform bone, Quaternion modelDelta)
        {
            if (!bone) return;
            var m = transform.rotation;
            bone.rotation = m * modelDelta * Quaternion.Inverse(m) * bone.rotation;
        }

        /// <summary>A bone's direction (root to tip) in the model's space, for droop/sway axes.</summary>
        public Vector3 ModelDir(Transform bone) => bone ? transform.InverseTransformDirection(bone.up) : Vector3.up;
    }
}
