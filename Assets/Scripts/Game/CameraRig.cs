using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>Frames the tower with the panel on the right, adds shake and a little idle drift.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public float ShakeScale = 1f;
        Vector3 basePos;
        Quaternion baseRot;
        float shake;
        float framedAspect;
        BuildingView framed;
        Vector3 pushTarget;
        float push, pushHold;

        public static CameraRig Create(Camera cam)
        {
            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Cam = cam;
            return rig;
        }

        public float Yaw = 9f, Pitch = 4f;

        /// <summary>Fit the whole tower (street to roof) in the middle of the screen, seen from a little right and above.</summary>
        public void Frame(BuildingView b)
        {
            framed = b;
            framedAspect = Cam.aspect;
            float bottom = -3.2f;
            float top = b.TopY + 3.6f;
            float h = top - bottom;
            float cy = (top + bottom) * 0.5f;
            float fov = Cam.fieldOfView * Mathf.Deg2Rad;
            float dist = h * 0.5f / Mathf.Tan(fov * 0.5f) * 1.06f;
            baseRot = Quaternion.Euler(Pitch, -Yaw, 0f);
            var center = new Vector3(0.2f, cy, 0.4f);
            basePos = center - baseRot * Vector3.forward * dist;
            // nudge so the tower sits between the left HUD column and the panel
            float visibleW = h * 1.06f * Cam.aspect;
            basePos += baseRot * Vector3.right * (visibleW * (0.5f - 0.505f));
            transform.position = basePos;
            transform.rotation = baseRot;
        }

        public void Shake(float amount) => shake = Mathf.Min(1.2f, shake + amount * ShakeScale);

        public void PushIn(Vector3 worldTarget, float amount, float hold)
        {
            pushTarget = worldTarget;
            push = Mathf.Max(push, 0.0001f);
            pushHold = hold;
            pushAmount = amount;
        }
        float pushAmount, pushK;

        void LateUpdate()
        {
            if (framed && Mathf.Abs(Cam.aspect - framedAspect) > 0.01f) Frame(framed);
            float dt = Time.deltaTime;
            shake = Mathf.Max(0f, shake - dt * 2.2f);
            pushHold -= dt;
            pushK = Mathf.Lerp(pushK, pushHold > 0f ? pushAmount : 0f, Ease.Damp(pushHold > 0f ? 3f : 2f, dt));
            float t = Time.unscaledTime;
            float s = shake * shake;
            var offset = new Vector3((Mathf.PerlinNoise(t * 23f, 1.3f) - 0.5f) * 1.1f, (Mathf.PerlinNoise(2.7f, t * 23f) - 0.5f) * 1.1f, 0f) * s;
            var drift = new Vector3(Mathf.Sin(t * 0.21f) * 0.25f, Mathf.Sin(t * 0.17f) * 0.18f, 0f);
            var pos = basePos + drift;
            if (pushK > 0.001f) pos = Vector3.Lerp(pos, pushTarget + (basePos - pushTarget).normalized * 22f, pushK);
            transform.position = pos + transform.rotation * offset;
            var look = Quaternion.LookRotation(Vector3.Lerp(baseRot * Vector3.forward, (pushTarget - pos).normalized, pushK));
            transform.rotation = look * Quaternion.Euler(0, 0, (Mathf.PerlinNoise(t * 17f, 5f) - 0.5f) * 1.6f * s);
        }
    }
}
