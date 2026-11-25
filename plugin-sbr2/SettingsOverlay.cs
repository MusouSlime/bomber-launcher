using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SBR2QoL
{
    // In-game settings overlay built from built-in uGUI components only.
    // No managed MonoBehaviour is injected: the canvas and its texts are
    // created from Unity types, and navigation is polled from the Harmony
    // tick while the overlay is open.
    internal static class SettingsOverlay
    {
        private sealed class Row
        {
            internal string Label;
            internal Func<string> Value;
            internal Action<int> Change;
        }

        private const int RowCount = 14;

        private static readonly List<Row> Rows = new List<Row>();
        private static readonly List<Text> Labels = new List<Text>();
        private static readonly List<Text> Values = new List<Text>();

        private static GameObject _root;
        private static Font _font;
        private static Sprite _solid;
        private static Text _title;
        private static Text _help;
        private static bool _built;
        private static bool _visible;
        private static int _selected;
        private static readonly List<Image> Highlights = new List<Image>();

        internal static bool Visible
        {
            get { return _visible; }
        }

        private static readonly string[] StoryCharacters =
        {
            "white", "black", "pink", "blue", "red", "yellow", "green",
            "aqua", "simon", "pyramid", "richter", "lance", "bill", "mimi",
            "nyami", "alucard", "vicviper", "nurse", "lobby", "tomato",
            "nakedSnake", "raiden", "oldsnake", "vicviperzero", "somaCruz",
            "fujisaki", "genso2p", "goemon", "magnet", "tinyGolem",
            "phantom", "sexy", "plasma", "fiter", "Massive", "wing", "bean",
            "lucaBlight"
        };

        private static readonly float[] FovSteps =
            { 0f, 60f, 70f, 75f, 80f, 85f, 90f, 100f, 110f };

        private static readonly float[] AutoSkipSteps =
            { 0f, 3f, 5f, 10f, 15f, 30f };

        internal static void Tick()
        {
            if (Plugin.OverlayEnabled.Value &&
                Input.GetKeyDown(Plugin.OverlayKey))
                Toggle();
            if (!_visible) return;

            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.S)) Move(1);
            else if (Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.W)) Move(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) ||
                Input.GetKeyDown(KeyCode.D)) Adjust(1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.A)) Adjust(-1);
            else if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter)) Adjust(1);
        }

        private static void Move(int dir)
        {
            if (Rows.Count == 0) return;
            _selected = (_selected + dir + Rows.Count) % Rows.Count;
            Refresh();
        }

        private static void Adjust(int dir)
        {
            if (_selected < 0 || _selected >= Rows.Count) return;
            var change = Rows[_selected].Change;
            if (change == null) return;
            try
            {
                change(dir);
                Plugin.SaveConfig();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("Overlay change: " + e.Message);
            }
            Refresh();
        }

        internal static void Toggle()
        {
            if (!EnsureBuilt()) return;
            _visible = !_visible;
            _root.SetActive(_visible);
            Plugin.Logger.LogInfo("Overlay " + (_visible ? "opened" : "closed"));
            if (_visible)
            {
                _selected = 0;
                Refresh();
            }
            else
            {
                Plugin.SaveConfig();
            }
        }

        private static bool EnsureBuilt()
        {
            if (_built) return _root != null;
            _built = true;
            try
            {
                Build();
                return _root != null;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("Overlay build failed: " + e);
                return false;
            }
        }

        private static void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont("Arial", 20);
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont("DejaVu Sans", 20);
            if (_font == null)
            {
                Plugin.Logger.LogWarning("Overlay: no usable font");
                return;
            }

            _root = new GameObject("SBR2QoL_Overlay");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _root.hideFlags = HideFlags.HideAndDontSave;

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Mathf.Max(1f, Screen.height / 1080f);

            _solid = SolidSprite();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_root.transform, false);
            var image = panel.AddComponent<Image>();
            image.sprite = _solid;
            image.type = Image.Type.Simple;
            image.color = new Color(0.02f, 0.02f, 0.04f, 1f);
            image.raycastTarget = false;
            SetRect(panel.GetComponent<RectTransform>(), 0.12f, 0.06f,
                0.88f, 0.94f);

            _title = MakeText(panel.transform, "Title", 30,
                TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.35f));
            _title.text = "SBR2 QoL Settings";
            SetRect(_title.rectTransform, 0.03f, 0.93f, 0.97f, 0.995f);

            _help = MakeText(panel.transform, "Help", 16,
                TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f));
            _help.text = "Up/Down select   Left/Right change   F9 close";
            SetRect(_help.rectTransform, 0.03f, 0.005f, 0.97f, 0.05f);

            BuildRows();

            for (int i = 0; i < RowCount; i++)
            {
                float top = 0.90f - i * 0.060f;
                var hlGo = new GameObject("H" + i);
                hlGo.transform.SetParent(panel.transform, false);
                var hl = hlGo.AddComponent<Image>();
                hl.sprite = _solid;
                hl.type = Image.Type.Simple;
                hl.color = new Color(1f, 0.85f, 0.3f, 0.22f);
                hl.raycastTarget = false;
                SetRect(hlGo.GetComponent<RectTransform>(), 0.03f,
                    top - 0.048f, 0.97f, top);
                Highlights.Add(hl);

                var label = MakeText(panel.transform, "L" + i, 22,
                    TextAnchor.MiddleLeft, Color.white);
                var value = MakeText(panel.transform, "V" + i, 22,
                    TextAnchor.MiddleRight, new Color(0.65f, 0.9f, 1f));
                SetRect(label.rectTransform, 0.04f, top - 0.046f, 0.62f, top);
                SetRect(value.rectTransform, 0.62f, top - 0.046f, 0.96f, top);
                Labels.Add(label);
                Values.Add(value);
            }

            _root.SetActive(false);
        }

        private static Sprite SolidSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private static Text MakeText(Transform parent, string name, int size,
            TextAnchor anchor, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetRect(RectTransform rect, float xMin,
            float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void BuildRows()
        {
            Rows.Clear();

            Rows.Add(Bool("BGM volume", () => Plugin.CustomBgmVolume.Value,
                v => Plugin.CustomBgmVolume.Value = v, 0.05f, true));
            Rows.Add(Toggle("BGM shuffle", () => Plugin.CustomBgmShuffle));
            Rows.Add(Toggle("BGM playlist", () => Plugin.CustomBgmPlaylist));
            Rows.Add(Toggle("Disable motion blur",
                () => Plugin.DisableMotionBlur));
            Rows.Add(Toggle("Disable depth of field",
                () => Plugin.DisableDepthOfField));
            Rows.Add(Toggle("Disable bloom", () => Plugin.DisableBloom));
            Rows.Add(Toggle("Disable vignette", () => Plugin.DisableVignette));
            Rows.Add(Toggle("Disable film grain", () => Plugin.DisableFilmGrain));
            Rows.Add(Toggle("Disable chromatic aberration",
                () => Plugin.DisableChromaticAberration));
            Rows.Add(Toggle("Disable lens distortion",
                () => Plugin.DisableLensDistortion));
            Rows.Add(Step("Field of view", FovSteps,
                () => Plugin.FieldOfView.Value,
                v => Plugin.FieldOfView.Value = v, "0 = vanilla"));
            Rows.Add(Toggle("Skip movies", () => Plugin.SkipMovies));
            Rows.Add(Step("Auto-skip movies", AutoSkipSteps,
                () => Plugin.AutoSkipMovies.Value,
                v => Plugin.AutoSkipMovies.Value = v, "0 = off"));
            Rows.Add(StoryRow());
        }

        private static Row Toggle(string label,
            Func<BepInEx.Configuration.ConfigEntry<bool>> entry)
        {
            return new Row
            {
                Label = label,
                Value = () => entry().Value ? "ON" : "OFF",
                Change = _ =>
                {
                    var e = entry();
                    e.Value = !e.Value;
                }
            };
        }

        private static Row Bool(string label,
            Func<float> get, Action<float> set, float step, bool percent)
        {
            return new Row
            {
                Label = label,
                Value = () => percent
                    ? Mathf.RoundToInt(get() * 100f) + "%"
                    : get().ToString("0.00"),
                Change = dir =>
                {
                    float v = Mathf.Clamp01(get() + dir * step);
                    set(v);
                }
            };
        }

        private static Row Step(string label, float[] steps,
            Func<float> get, Action<float> set, string zeroText)
        {
            return new Row
            {
                Label = label,
                Value = () =>
                {
                    float v = get();
                    if (v <= 0.001f) return zeroText;
                    return v.ToString("0.##");
                },
                Change = dir =>
                {
                    float v = get();
                    int index = 0;
                    float best = float.MaxValue;
                    for (int i = 0; i < steps.Length; i++)
                    {
                        float d = Mathf.Abs(steps[i] - v);
                        if (d < best)
                        {
                            best = d;
                            index = i;
                        }
                    }
                    index = (index + dir + steps.Length) % steps.Length;
                    set(steps[index]);
                }
            };
        }

        private static Row StoryRow()
        {
            return new Row
            {
                Label = "Story character",
                Value = () =>
                {
                    string key = Plugin.StoryCharacterKey.Value ?? "";
                    return string.IsNullOrEmpty(key) ? "Default (White)" : key;
                },
                Change = dir =>
                {
                    string current = Plugin.StoryCharacterKey.Value ?? "";
                    int index = Array.FindIndex(StoryCharacters,
                        x => string.Equals(x, current,
                            StringComparison.OrdinalIgnoreCase));
                    if (index < 0) index = 0;
                    index = (index + dir + StoryCharacters.Length) %
                        StoryCharacters.Length;
                    Plugin.StoryCharacterKey.Value = StoryCharacters[index];
                }
            };
        }

        private static void Refresh()
        {
            for (int i = 0; i < Labels.Count; i++)
            {
                bool selected = i == _selected && i < Rows.Count;
                Labels[i].text = (selected ? "> " : "   ") +
                    (i < Rows.Count ? Rows[i].Label : "");
                Labels[i].color = selected
                    ? new Color(1f, 0.86f, 0.35f)
                    : Color.white;
                Values[i].text = i < Rows.Count && Rows[i].Value != null
                    ? Rows[i].Value()
                    : "";
                if (i < Highlights.Count)
                    Highlights[i].color = selected
                        ? new Color(0.45f, 0.38f, 0.08f, 0.55f)
                        : new Color(0, 0, 0, 0f);
            }
            if (_title != null)
                _title.text = "SBR2 QoL Settings";
        }
    }
}
