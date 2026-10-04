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
        /// <summary>Where the tower's centre sits horizontally on screen (0..1).</summary>
        public float ScreenX = 0.505f;
        Vector3 targetPos;
        Quaternion targetRot;
        bool hasFrame;

        /// <summary>Fit the whole tower (street to roof) in the middle of the screen, seen from a little right and above.</summary>
        public void Frame(BuildingView b)
        {
            framed = b;
            framedAspect = Cam.aspect;
            float bottom = -2.7f;
            float top = b.TopY + 4.5f;
            float h = top - bottom;
            float cy = (top + bottom) * 0.5f;
            float fov = Cam.fieldOfView * Mathf.Deg2Rad;
            float dist = h * 0.5f / Mathf.Tan(fov * 0.5f) * 1.06f;
            baseRot = Quaternion.Euler(Pitch, -Yaw, 0f);
            var center = new Vector3(0.2f, cy, 0.4f);
            basePos = center - baseRot * Vector3.forward * dist;
            // nudge so the tower sits where the layout wants it (between HUD and panel, or right of the menu)
            float visibleW = h * 1.06f * Cam.aspect;
            basePos += baseRot * Vector3.right * (visibleW * (0.5f - ScreenX));
            targetPos = basePos;
            targetRot = baseRot;
            if (!hasFrame) { transform.position = basePos; transform.rotation = baseRot; framedPos = basePos; framedRot = baseRot; hasFrame = true; }
        }

        Vector3 framedPos;
        Quaternion framedRot;
        float lastScreenX = -1f;

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
            if (framed && (Mathf.Abs(Cam.aspect - framedAspect) > 0.01f || ScreenX != lastScreenX)) { lastScreenX = ScreenX; Frame(framed); }
            float dt = UiTime.Dt;
            // glide between framings (menus <-> play)
            framedPos = Vector3.Lerp(framedPos, targetPos, Ease.Damp(3.5f, dt));
            framedRot = Quaternion.Slerp(framedRot, targetRot, Ease.Damp(3.5f, dt));
            basePos = framedPos;
            baseRot = framedRot;
            shake = Mathf.Max(0f, shake - dt * 2.2f);
            pushHold -= dt;
            pushK = Mathf.Lerp(pushK, pushHold > 0f ? pushAmount : 0f, Ease.Damp(pushHold > 0f ? 3f : 2f, dt));
            float t = UiTime.Now;
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
