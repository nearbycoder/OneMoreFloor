using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace OneMoreFloor.Tests
{
    /// <summary>
    /// The UI's text shadows and outlines are shader_features of TMP's mobile SDF shader, and a build only keeps the
    /// keyword sets that some material in it uses. Every set the UI code asks for has to be kept by a material under
    /// Resources, or the build quietly draws the text without it.
    /// </summary>
    public class TextVariantTests
    {
        static readonly string[] Features = { "OUTLINE_ON", "UNDERLAY_ON", "UNDERLAY_INNER" };

        static string Set(Material m) => string.Join(" ", Features.Where(m.IsKeywordEnabled));

        static HashSet<string> Kept(Shader shader)
        {
            var kept = new HashSet<string>();
            foreach (var m in Resources.LoadAll<Material>("")) if (m.shader == shader) kept.Add(Set(m));
            return kept;
        }

        readonly List<GameObject> made = new List<GameObject>();

        [TearDown]
        public void CleanUp()
        {
            foreach (var go in made) Object.DestroyImmediate(go);
            made.Clear();
        }

        TextMeshProUGUI Text(TMP_FontAsset font)
        {
            var parent = new GameObject("TextVariantTests", typeof(RectTransform));
            made.Add(parent);
            return UiKit.Text("T", parent.transform, "Paused", 40, Color.white, font, TextAlignmentOptions.Center, new Vector2(400, 60));
        }

        [Test]
        public void EveryTextStyleTheUiAsksForIsKeptInTheBuild()
        {
            var shader = UiKit.Display.material.shader;
            var kept = Kept(shader);
            var asked = new Dictionary<string, Material>
            {
                ["Shadowed"] = Deco.Shadowed(Text(UiKit.Body)).fontMaterial,
                ["Gilded"] = Deco.Gilded(Text(UiKit.Display)).fontMaterial,
                ["Outlined + Shadowed (banner)"] = Deco.Outlined(Deco.Shadowed(Text(UiKit.Display)), 0.18f, new Color32(30, 20, 34, 255)).fontMaterial,
                ["popup"] = Hud.PopupMaterial(),
                ["plain"] = Text(UiKit.Signage).fontSharedMaterial,
            };
            foreach (var kv in asked)
            {
                Assert.AreEqual(shader, kv.Value.shader, kv.Key);
                Assert.IsTrue(kept.Contains(Set(kv.Value)),
                    $"{kv.Key} asks for '{Set(kv.Value)}', but the build only keeps: {string.Join(", ", kept.Select(k => $"'{k}'"))}");
            }
        }

        [Test]
        public void GildedTypeDrawsItsOutlineAndShadow()
        {
            var m = Deco.Gilded(Text(UiKit.Display)).fontMaterial;
            Assert.IsTrue(m.IsKeywordEnabled("OUTLINE_ON"), "outline");
            Assert.IsTrue(m.IsKeywordEnabled("UNDERLAY_ON"), "shadow");
            Assert.Greater(m.GetFloat(ShaderUtilities.ID_OutlineWidth), 0f);
        }
    }
}
