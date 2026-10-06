using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Frames the tower with the panel on the right, adds shake and a little idle drift. During play the
    /// player can zoom in (scroll wheel, Z, gamepad triggers) to a close-up that follows the car.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public float ShakeScale = 1f;
        /// <summary>Reduced motion: no idle drift and no push-ins (shake is off through <see cref="ShakeScale"/>).</summary>
        public bool Still;
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

        /// <summary>0 = the whole tower, 1 = the closest view (about five floors), following the car.</summary>
        public float Zoom { get => zoomTarget; set => zoomTarget = Mathf.Clamp01(value); }
        /// <summary>Zooming only applies during play; menus always show the whole tower.</summary>
        public bool AllowZoom;
        /// <summary>World height the close-up keeps in view (the car).</summary>
        public float FollowY;
        public float ZoomShown => zoomShown;
        float zoomTarget, zoomShown;
        float frameBottom, frameTop;
        /// <summary>World height of the close-up view: about five floors plus margins.</summary>
        public const float CloseHeight = 16.5f;

        /// <summary>Fit the whole tower (street to roof) in the middle of the screen, seen from a little right and above.</summary>
        public void Frame(BuildingView b)
        {
            framed = b;
            framedAspect = Cam.aspect;
            frameBottom = -2.7f;
            frameTop = b.TopY + 4.5f;
            Retarget();
            if (!hasFrame) { transform.position = targetPos; transform.rotation = targetRot; framedPos = targetPos; framedRot = targetRot; hasFrame = true; }
        }

        /// <summary>
        /// During play: the free strip of screen between the HUD card (plus the floor-label gutter) and the panel,
        /// as fractions of the screen width (x = left, y = right). Zero = not set.
        /// </summary>
        public Vector2 PlayBand;
        /// <summary>Tower width on screen in world units: the walls plus the side the camera's yaw shows.</summary>
        const float TowerWidth = 18f;
        /// <summary>
        /// Play framing: down to the roof cornice (the neon sign is for the menus) and a strip of street. With the gamepad prompt strip on screen the street
        /// grows so the strip sits under the tower, not over the bottom floor.
        /// </summary>
        const float PlayRoof = 1.35f, PlayStreet = -1.0f, PromptStreet = -2.4f;
        /// <summary>The gamepad / arrow-key prompt strip is showing along the bottom.</summary>
        public bool RoomForPrompts;
        float promptK;

        /// <summary>Where the camera should be for the current framing, zoom and follow point.</summary>
        void Retarget()
        {
            // menus show the whole tower, street to water tower. Play frames it tighter so floors and guests are
            // bigger, but never wider than the strip between the HUD and the panel.
            bool play = AllowZoom && framed;
            float bottom = play ? Mathf.Lerp(PlayStreet, PromptStreet, promptK) : frameBottom, top = play ? framed.TopY + PlayRoof : frameTop;
            float margin = play ? 1f : 1.06f;
            float screenX = ScreenX;
            if (play && PlayBand.y > PlayBand.x)
            {
                float fit = TowerWidth / ((PlayBand.y - PlayBand.x) * Cam.aspect);
                // too wide for the strip (narrow screens, short towers). A little extra height goes to the street so
                // the roof sign stays out of frame rather than half in; a lot shows the whole roof, then centres.
                float extra = fit - (top - bottom);
                if (extra > 0f)
                {
                    float roofRoom = frameTop - top;
                    if (extra <= roofRoom) bottom -= extra;
                    else { top = frameTop; extra -= roofRoom; bottom -= extra * 0.5f; top += extra * 0.5f; }
                }
                screenX = (PlayBand.x + PlayBand.y) * 0.5f;
            }
            float full = top - bottom;
            float k = AllowZoom ? Ease.InOutCubic(zoomShown) : 0f;
            float h = Mathf.Lerp(full, Mathf.Min(full, CloseHeight), k);
            float cy = Mathf.Clamp(FollowY, bottom + h * 0.5f, top - h * 0.5f);
            if (k <= 0f) cy = (top + bottom) * 0.5f;
            float fov = Cam.fieldOfView * Mathf.Deg2Rad;
            float dist = h * 0.5f / Mathf.Tan(fov * 0.5f) * margin;
            targetRot = Quaternion.Euler(Pitch, -Yaw, 0f);
            var center = new Vector3(0.2f, cy, 0.4f);
            targetPos = center - targetRot * Vector3.forward * dist;
            // nudge so the tower sits where the layout wants it (between HUD and panel, or right of the menu)
            float visibleW = h * margin * Cam.aspect;
            targetPos += targetRot * Vector3.right * (visibleW * (0.5f - screenX));
        }

        Vector3 framedPos;
        Quaternion framedRot;
        float lastScreenX = -1f;

        public void Shake(float amount) => shake = Mathf.Min(1.2f, shake + amount * ShakeScale);

        public void PushIn(Vector3 worldTarget, float amount, float hold)
        {
            if (Still) return;
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
            zoomShown = Mathf.MoveTowards(zoomShown, AllowZoom ? zoomTarget : 0f, dt * 2.2f);
            promptK = Mathf.MoveTowards(promptK, RoomForPrompts ? 1f : 0f, dt * 2.5f);
            if (framed) Retarget();
            // glide between framings (menus <-> play) and follow the car when zoomed
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
            var drift = Still ? Vector3.zero : new Vector3(Mathf.Sin(t * 0.21f) * 0.25f, Mathf.Sin(t * 0.17f) * 0.18f, 0f);
            var pos = basePos + drift;
            if (pushK > 0.001f) pos = Vector3.Lerp(pos, pushTarget + (basePos - pushTarget).normalized * 22f, pushK);
            transform.position = pos + transform.rotation * offset;
            var look = Quaternion.LookRotation(Vector3.Lerp(baseRot * Vector3.forward, (pushTarget - pos).normalized, pushK));
            transform.rotation = look * Quaternion.Euler(0, 0, (Mathf.PerlinNoise(t * 17f, 5f) - 0.5f) * 1.6f * s);
        }
    }
}
