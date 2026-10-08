using System.Collections;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// -omfFidelityShots dir [shift]: plays a shift with the bot for a while, freezes one moment (game time stopped,
    /// camera drift off) and saves the back buffer at every GRAPHICS FIDELITY step, LOW to ULTRA, so the captures
    /// differ only by the setting. It does it twice: the whole tower, then the close-up. It also logs how many
    /// particles a 20-particle burst makes at each step. Then it quits. Runs on a throwaway save.
    /// -omfSkyShots dir [shift]: the same frozen moment under every lighting preset, morning to night (add -omfNoCity
    /// for the backdrop without the city, as it was before round 12).
    /// </summary>
    public sealed class FidelityShots : MonoBehaviour
    {
        string dir;
        int shift = 1;
        bool sky;

        public static void TryStart(GameRoot root)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-omfFidelityShots");
            bool sky = false;
            if (i < 0) { i = System.Array.IndexOf(args, "-omfSkyShots"); sky = true; }
            if (i < 0 || i + 1 >= args.Length) return;
            var p = root.gameObject.AddComponent<FidelityShots>();
            p.dir = args[i + 1];
            p.sky = sky;
            if (i + 2 < args.Length) int.TryParse(args[i + 2], out p.shift);
        }

        IEnumerator Start()
        {
            if (sky) return SkyShots();
            return FidelitySteps();
        }

        IEnumerator SkyShots()
        {
            SaveData.Ephemeral = true;
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(1f);
            var root = GameRoot.Instance;
            root.StartShift(shift, 4242);
            root.Runner.AutoBot = Bot.Decent(3);
            root.Rig.Still = true;
            yield return new WaitForSecondsRealtime(6f);
            Time.timeScale = 0f;
            foreach (var id in Sky.Ids)
            {
                root.Sky.Apply(id, root.Sun, root.WorldCam);
                for (int f = 0; f < 10; f++) yield return null;
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                string name = System.IO.Path.Combine(dir, $"sky-{System.Array.IndexOf(Sky.Ids, id)}-{id}.png");
                System.IO.File.WriteAllBytes(name, tex.EncodeToPNG());
                Destroy(tex);
                Debug.Log($"[Fidelity] sky {id} {Screen.width}x{Screen.height} saved to {name}");
            }
            Time.timeScale = 1f;
            Debug.Log("[Fidelity] done");
            Application.Quit(0);
        }

        IEnumerator FidelitySteps()
        {
            SaveData.Ephemeral = true;
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(1f);
            var root = GameRoot.Instance;
            root.StartShift(shift, 4242);
            root.Runner.AutoBot = Bot.Decent(3);
            root.Rig.Still = true;   // no idle drift, so the frozen frames line up
            yield return new WaitForSecondsRealtime(14f);
            yield return Freeze(root, "tower");
            root.Rig.Zoom = 1f;
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Freeze(root, "closeup");
            GameRoot.GraphicsOverride = -1;
            Debug.Log("[Fidelity] done");
            Application.Quit(0);
        }

        IEnumerator Freeze(GameRoot root, string view)
        {
            Time.timeScale = 0f;
            for (int lv = GraphicsQuality.Low; lv <= GraphicsQuality.Ultra; lv++)
            {
                GameRoot.GraphicsOverride = lv;
                root.ApplySettings();
                // let the pipeline rebuild its targets and every lamp pick up its shadows
                for (int f = 0; f < 20; f++) yield return null;
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                string name = System.IO.Path.Combine(dir, $"{view}-{lv}-{GraphicsQuality.Names[lv].ToLowerInvariant()}.png");
                System.IO.File.WriteAllBytes(name, tex.EncodeToPNG());
                Destroy(tex);
                Debug.Log($"[Fidelity] {GraphicsQuality.Names[lv]} {view} {Screen.width}x{Screen.height} saved to {name}");
                if (view == "tower") Debug.Log($"[Fidelity] {GraphicsQuality.Names[lv]}: a 20-particle burst makes {Fx.Scaled(20)} particles");
            }
            Time.timeScale = 1f;
        }
    }
}
