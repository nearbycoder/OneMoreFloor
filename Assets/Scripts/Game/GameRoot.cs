using OneMoreFloor.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>Bootstrap: the Main scene only contains this. Everything else is built at runtime.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        public const int UiLayer = 5;

        public Camera WorldCam { get; private set; }
        public Camera UiCam { get; private set; }
        public Canvas Canvas { get; private set; }
        public ShiftRunner Runner { get; private set; }
        public CameraRig Rig { get; private set; }
        public BuildingView Building { get; private set; }
        public Light Sun { get; private set; }
        public Sky Sky { get; private set; }
        public Volume Post { get; private set; }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            BuildWorld();
            BuildUi();
        }

        void Start()
        {
            StartShift(StartIndexFromArgs(), 1);
        }

        static int StartIndexFromArgs()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-omfShift" && int.TryParse(args[i + 1], out int s)) return Mathf.Clamp(s, 0, ShiftCatalog.All.Count - 1);
            return 0;
        }

        void BuildWorld()
        {
            var camGo = new GameObject("WorldCamera");
            camGo.tag = "MainCamera";
            WorldCam = camGo.AddComponent<Camera>();
            WorldCam.fieldOfView = 24f;
            WorldCam.nearClipPlane = 1f;
            WorldCam.farClipPlane = 600f;
            WorldCam.cullingMask = ~(1 << UiLayer);
            WorldCam.clearFlags = CameraClearFlags.SolidColor;
            WorldCam.backgroundColor = Palette.Hex(0x23324F);
            var wdata = WorldCam.GetUniversalAdditionalCameraData();
            wdata.renderPostProcessing = true;
            wdata.antialiasing = AntialiasingMode.None;
            Rig = CameraRig.Create(WorldCam);

            var sunGo = new GameObject("Sun");
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.75f;
            Sun.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

            Sky = Sky.Create(transform);

            var volGo = new GameObject("Post");
            Post = volGo.AddComponent<Volume>();
            Post.isGlobal = true;
            Post.priority = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.65f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.contrast.Override(10f);
            color.postExposure.Override(0.15f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.45f);
            Post.profile = profile;

            Building = BuildingView.Create(transform);
            var car = CarView.Create(Building.transform);

            var runnerGo = new GameObject("Runner");
            runnerGo.transform.SetParent(transform, false);
            Runner = runnerGo.AddComponent<ShiftRunner>();
            Runner.Building = Building;
            Runner.Car = car;
            Runner.Rig = Rig;
            Runner.Cam = WorldCam;
        }

        void BuildUi()
        {
            var uiGo = new GameObject("UiCamera");
            UiCam = uiGo.AddComponent<Camera>();
            UiCam.orthographic = true;
            UiCam.cullingMask = 1 << UiLayer;
            UiCam.clearFlags = CameraClearFlags.Depth;
            UiCam.nearClipPlane = 0.1f;
            UiCam.farClipPlane = 50f;
            uiGo.transform.position = new Vector3(0, -1000, 0);
            var udata = UiCam.GetUniversalAdditionalCameraData();
            udata.renderType = CameraRenderType.Overlay;
            udata.renderPostProcessing = false;
            WorldCam.GetUniversalAdditionalCameraData().cameraStack.Add(UiCam);

            var canvasGo = new GameObject("Canvas");
            canvasGo.layer = UiLayer;
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = UiCam;
            Canvas.planeDistance = 10f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            Runner.Hud = Hud.Create(Canvas.transform, Runner, WorldCam);
            SetLayerRecursive(canvasGo, UiLayer);
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        public void StartShift(int index, ulong seed)
        {
            var def = ShiftCatalog.Get(index);
            Sky.Apply(def.Lighting, Sun, WorldCam);
            Runner.Begin(def, seed);
            SetLayerRecursive(Canvas.gameObject, UiLayer);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            // developer shortcuts: F1..F10 start a shift
            for (int i = 0; i < 10; i++)
            {
                var k = kb[Key.F1 + i];
                if (k != null && k.wasPressedThisFrame) StartShift(i, (ulong)System.DateTime.Now.Ticks);
            }
            if (kb.f12Key.wasPressedThisFrame)
                Shots.Capture(System.IO.Path.Combine(Application.persistentDataPath, $"shot_{System.DateTime.Now:HHmmss}.png"));
        }
    }
}
