using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SBR2QoL
{
    // In-game settings overlay with tabs, built from built-in uGUI components.
    // No managed MonoBehaviour is injected: the canvas and texts are created
    // from Unity types and driven by the QoL tick / InputAdapter.
    internal static class SettingsOverlay
    {
        private sealed class Row
        {
            internal string Tab;
            internal string Label;
            internal Func<string> Value;
            internal Action<int> Change;
        }

        private static readonly string[] Tabs =
            { "Audio", "Graphics", "Movies", "Story", "Options" };

        private const int MaxRows = 9;

        private static readonly List<Row> Rows = new List<Row>();
        private static readonly List<Row> VisibleRows = new List<Row>();
        private static readonly List<Text> Labels = new List<Text>();
        private static readonly List<Text> Values = new List<Text>();
        private static readonly List<Image> Highlights = new List<Image>();
        private static readonly List<Text> TabLabels = new List<Text>();

        private static GameObject _root;
        private static Font _font;
        private static Sprite _solid;
        private static Text _title;
        private static Text _help;
        private static bool _built;
        private static bool _visible;
        private static int _selected;
        private static int _tabIndex;

        private static bool _inputChecked;
        private static InputAdapter _input;
        private static bool _padLogged;
        private static bool _padSeen;
        private static int _padIndex = -1;
        private static bool _inputActiveSaved;
        private static bool _inputActiveBefore;
        private static bool _prevUp;
        private static bool _prevDown;
        private static bool _prevLeft;
        private static bool _prevRight;
        private static bool _prevSubmit;
        private static bool _prevTabL;
        private static bool _prevTabR;
        private static float _padIgnoreUntil;
        private static string _lastAction;
        private static float _lastActionAt;

        private static bool _axesChecked;
        private static bool _axesOk;
        private static bool _dpadOk;
        private static float _nextVertical;
        private static float _nextHorizontal;
        private static bool _submitChecked;
        private static bool _submitOk;
        private static bool _cancelChecked;
        private static bool _cancelOk;

        internal static float BypassUntil;
        internal static bool ReadingPad;

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

        private static readonly string[] SkipKeys =
            { "Space", "Escape", "Return", "F8" };

        private static readonly string[] OverlayKeys =
            { "F9", "F8", "F10", "F11", "Insert" };

        internal static void Tick()
        {
            if (Plugin.OverlayEnabled.Value &&
                Input.GetKeyDown(Plugin.OverlayKey))
                Toggle();
            if (!_visible) return;

            bool shift = Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.Tab))
                SwitchTab(shift ? -1 : 1);
            else if (Input.GetKeyDown(KeyCode.Q) ||
                     Input.GetKeyDown(KeyCode.PageUp))
                SwitchTab(-1);
            else if (Input.GetKeyDown(KeyCode.E) ||
                     Input.GetKeyDown(KeyCode.PageDown))
                SwitchTab(1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) ||
                     Input.GetKeyDown(KeyCode.S))
            {
                if (AllowAction("down")) Move(1);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) ||
                     Input.GetKeyDown(KeyCode.W))
            {
                if (AllowAction("up")) Move(-1);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) ||
                     Input.GetKeyDown(KeyCode.D))
            {
                if (AllowAction("right")) Adjust(1);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) ||
                     Input.GetKeyDown(KeyCode.A))
            {
                if (AllowAction("left")) Adjust(-1);
            }
            else if (Input.GetKeyDown(KeyCode.Return) ||
                     Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (AllowAction("submit")) Adjust(1);
            }

            PollController();
        }

        private static void Move(int dir)
        {
            if (VisibleRows.Count == 0) return;
            _selected = (_selected + dir + VisibleRows.Count) %
                VisibleRows.Count;
            Refresh();
        }

        private static void Adjust(int dir)
        {
            if (_selected < 0 || _selected >= VisibleRows.Count) return;
            var change = VisibleRows[_selected].Change;
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

        private static void SwitchTab(int dir)
        {
            _tabIndex = (_tabIndex + dir + Tabs.Length) % Tabs.Length;
            _selected = 0;
            Refresh();
        }

        private static void RebuildVisibleRows()
        {
            VisibleRows.Clear();
            string tab = Tabs[_tabIndex];
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Tab == tab) VisibleRows.Add(Rows[i]);
            }
            if (VisibleRows.Count == 0) _selected = 0;
            else if (_selected >= VisibleRows.Count)
                _selected = VisibleRows.Count - 1;
        }

        private static bool AllowAction(string action)
        {
            float now = Time.unscaledTime;
            if (_lastAction == action && now - _lastActionAt < 0.15f)
                return false;
            _lastAction = action;
            _lastActionAt = now;
            return true;
        }

        private static void EnsureInput()
        {
            if (_inputChecked) return;
            _inputChecked = true;
            try { _input = UnityEngine.Object.FindObjectOfType<InputAdapter>(); }
            catch { _input = null; }
            Plugin.Logger.LogInfo("Overlay input: " +
                (_input == null ? "no InputAdapter" : "InputAdapter found"));
        }

        private static bool PadEvent(InputAdapter.EVENT e)
        {
            ReadingPad = true;
            try
            {
                return _input != null && _input.getEvent(_padIndex, e);
            }
            finally
            {
                ReadingPad = false;
            }
        }

        private static bool Edge(ref bool previous, bool current)
        {
            bool rising = current && !previous;
            previous = current;
            return rising;
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
                _padIgnoreUntil = Time.unscaledTime + 0.8f;
                SyncPadState();
                Refresh();
                if (!Plugin.GamepadNavigation.Value)
                {
                    EnsureInput();
                    if (_input != null)
                    {
                        _inputActiveBefore = _input._isInputActive;
                        _input.setInputActive(false);
                        _inputActiveSaved = true;
                    }
                }
            }
            else
            {
                if (_inputActiveSaved && _input != null)
                {
                    _input.setInputActive(_inputActiveBefore);
                    _inputActiveSaved = false;
                }
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
            SetRect(panel.GetComponent<RectTransform>(), 0.10f, 0.06f,
                0.90f, 0.94f);

            _title = MakeText(panel.transform, "Title", 28,
                TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.35f));
            _title.text = "SBR2 QoL Settings";
            SetRect(_title.rectTransform, 0.03f, 0.925f, 0.97f, 0.985f);

            for (int i = 0; i < Tabs.Length; i++)
            {
                float x = 0.025f + i * 0.195f;
                var tab = MakeText(panel.transform, "Tab" + i, 19,
                    TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.75f));
                SetRect(tab.rectTransform, x, 0.875f, x + 0.19f, 0.925f);
                TabLabels.Add(tab);
            }

            BuildRows();

            for (int i = 0; i < MaxRows; i++)
            {
                float top = 0.855f - i * 0.075f;
                var hlGo = new GameObject("H" + i);
                hlGo.transform.SetParent(panel.transform, false);
                var hl = hlGo.AddComponent<Image>();
                hl.sprite = _solid;
                hl.type = Image.Type.Simple;
                hl.color = new Color(1f, 0.85f, 0.3f, 0f);
                hl.raycastTarget = false;
                SetRect(hlGo.GetComponent<RectTransform>(), 0.03f,
                    top - 0.066f, 0.97f, top);
                Highlights.Add(hl);

                var label = MakeText(panel.transform, "L" + i, 22,
                    TextAnchor.MiddleLeft, Color.white);
                var value = MakeText(panel.transform, "V" + i, 22,
                    TextAnchor.MiddleRight, new Color(0.65f, 0.9f, 1f));
                SetRect(label.rectTransform, 0.05f, top - 0.064f, 0.60f, top);
                SetRect(value.rectTransform, 0.60f, top - 0.064f, 0.95f, top);
                Labels.Add(label);
                Values.Add(value);
            }

            _help = MakeText(panel.transform, "Help", 16,
                TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f));
            _help.text =
                "Q/E or Tab: tabs   Up/Down: row   Left/Right: change   F9: close";
            SetRect(_help.rectTransform, 0.03f, 0.005f, 0.97f, 0.055f);

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

            Rows.Add(Bool("Audio", "BGM volume",
                () => Plugin.CustomBgmVolume.Value,
                v => Plugin.CustomBgmVolume.Value = v, 0.05f, true));
            Rows.Add(Toggle("Audio", "BGM shuffle", () => Plugin.CustomBgmShuffle));
            Rows.Add(Toggle("Audio", "BGM playlist", () => Plugin.CustomBgmPlaylist));
            Rows.Add(Bool("Audio", "Explosion volume",
                () => Plugin.ExplosionSfxVolume.Value,
                v => Plugin.ExplosionSfxVolume.Value = v, 0.05f, true));
            Rows.Add(Toggle("Audio", "Explosion shuffle",
                () => Plugin.ExplosionSfxShuffle));

            Rows.Add(Toggle("Graphics", "Disable motion blur",
                () => Plugin.DisableMotionBlur));
            Rows.Add(Toggle("Graphics", "Disable depth of field",
                () => Plugin.DisableDepthOfField));
            Rows.Add(Toggle("Graphics", "Disable bloom",
                () => Plugin.DisableBloom));
            Rows.Add(Toggle("Graphics", "Disable vignette",
                () => Plugin.DisableVignette));
            Rows.Add(Toggle("Graphics", "Disable film grain",
                () => Plugin.DisableFilmGrain));
            Rows.Add(Toggle("Graphics", "Disable chromatic aberration",
                () => Plugin.DisableChromaticAberration));
            Rows.Add(Toggle("Graphics", "Disable lens distortion",
                () => Plugin.DisableLensDistortion));
            Rows.Add(Step("Graphics", "Field of view", FovSteps,
                () => Plugin.FieldOfView.Value,
                v => Plugin.FieldOfView.Value = v, "0 = vanilla"));
            Rows.Add(PerformanceRow());

            Rows.Add(Toggle("Movies", "Skip movies", () => Plugin.SkipMovies));
            Rows.Add(new Row
            {
                Tab = "Movies",
                Label = "Skip key",
                Value = () => Plugin.SkipKeyName.Value,
                Change = dir =>
                {
                    int index = Array.FindIndex(SkipKeys, k =>
                        string.Equals(k, Plugin.SkipKeyName.Value,
                            StringComparison.OrdinalIgnoreCase));
                    if (index < 0) index = 0;
                    index = (index + dir + SkipKeys.Length) % SkipKeys.Length;
                    Plugin.SkipKeyName.Value = SkipKeys[index];
                    Plugin.SkipKey = (KeyCode)Enum.Parse(
                        typeof(KeyCode), SkipKeys[index], true);
                }
            });
            Rows.Add(Step("Movies", "Auto-skip movies", AutoSkipSteps,
                () => Plugin.AutoSkipMovies.Value,
                v => Plugin.AutoSkipMovies.Value = v, "0 = off"));

            Rows.Add(StoryRow());

            Rows.Add(new Row
            {
                Tab = "Options",
                Label = "Close overlay",
                Value = () => ">",
                Change = _ => Toggle()
            });
            Rows.Add(new Row
            {
                Tab = "Options",
                Label = "Open game options",
                Value = () => ">",
                Change = _ => OpenGameOptions()
            });
            Rows.Add(Toggle("Options", "Replace game options",
                () => Plugin.ReplaceOptionsMenu));
            Rows.Add(new Row
            {
                Tab = "Options",
                Label = "Overlay key",
                Value = () => Plugin.OverlayKeyName.Value,
                Change = dir =>
                {
                    int index = Array.FindIndex(OverlayKeys, k =>
                        string.Equals(k, Plugin.OverlayKeyName.Value,
                            StringComparison.OrdinalIgnoreCase));
                    if (index < 0) index = 0;
                    index = (index + dir + OverlayKeys.Length) %
                        OverlayKeys.Length;
                    Plugin.OverlayKeyName.Value = OverlayKeys[index];
                    Plugin.OverlayKey = (KeyCode)Enum.Parse(
                        typeof(KeyCode), OverlayKeys[index], true);
                }
            });
            Rows.Add(Toggle("Options", "Controller navigation",
                () => Plugin.GamepadNavigation));
            Rows.Add(new Row
            {
                Tab = "Options",
                Label = "Test explosion",
                Value = () => ">",
                Change = _ => ExplosionSfx.PlayTest()
            });
            Rows.Add(new Row
            {
                Tab = "Options",
                Label = "Controller",
                Value = () => "Stick + A / B",
                Change = null
            });
        }

        private static Row Toggle(string tab, string label,
            Func<BepInEx.Configuration.ConfigEntry<bool>> entry)
        {
            return new Row
            {
                Tab = tab,
                Label = label,
                Value = () => entry().Value ? "ON" : "OFF",
                Change = _ =>
                {
                    var e = entry();
                    e.Value = !e.Value;
                }
            };
        }

        private static Row Bool(string tab, string label,
            Func<float> get, Action<float> set, float step, bool percent)
        {
            return new Row
            {
                Tab = tab,
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

        private static Row Step(string tab, string label, float[] steps,
            Func<float> get, Action<float> set, string zeroText)
        {
            return new Row
            {
                Tab = tab,
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

        private static Row PerformanceRow()
        {
            return new Row
            {
                Tab = "Graphics",
                Label = "Performance",
                Value = () =>
                {
                    switch (PerformanceTiers.Current())
                    {
                        case "high": return "High-End";
                        case "ultra": return "Ultra-Low";
                        case "low": return "Low-End";
                        default: return "Off";
                    }
                },
                Change = dir =>
                {
                    string[] tiers = { "off", "low", "ultra", "high" };
                    int index = Array.IndexOf(tiers,
                        PerformanceTiers.Current());
                    if (index < 0) index = 0;
                    index = (index + dir + tiers.Length) % tiers.Length;
                    PerformanceTiers.SetTier(tiers[index]);
                }
            };
        }

        private static Row StoryRow()
        {
            return new Row
            {
                Tab = "Story",
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

        internal static void OpenGameOptions()
        {
            try
            {
                _visible = false;
                if (_root != null) _root.SetActive(false);
                BypassUntil = Time.unscaledTime + 1f;
                var menu = UnityEngine.Object.FindObjectOfType<MainMenuUI>();
                if (menu == null)
                {
                    Plugin.Logger.LogWarning(
                        "OpenGameOptions: main menu not active");
                    return;
                }
                menu.onSubmitOther();
                Plugin.Logger.LogInfo("Overlay: opening vanilla game options");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("OpenGameOptions: " + e.Message);
            }
        }

        private static void Refresh()
        {
            RebuildVisibleRows();

            for (int i = 0; i < TabLabels.Count; i++)
            {
                bool active = i == _tabIndex;
                TabLabels[i].text = Tabs[i];
                TabLabels[i].color = active
                    ? new Color(1f, 0.86f, 0.35f)
                    : new Color(0.55f, 0.6f, 0.7f);
            }

            for (int i = 0; i < Labels.Count; i++)
            {
                if (i < VisibleRows.Count)
                {
                    bool selected = i == _selected;
                    Labels[i].text = (selected ? "> " : "   ") +
                        VisibleRows[i].Label;
                    Labels[i].color = selected
                        ? new Color(1f, 0.86f, 0.35f)
                        : Color.white;
                    Values[i].text = VisibleRows[i].Value != null
                        ? VisibleRows[i].Value()
                        : "";
                    if (i < Highlights.Count)
                        Highlights[i].color = selected
                            ? new Color(0.45f, 0.38f, 0.08f, 0.55f)
                            : new Color(0, 0, 0, 0f);
                }
                else
                {
                    Labels[i].text = "";
                    Values[i].text = "";
                    if (i < Highlights.Count)
                        Highlights[i].color = new Color(0, 0, 0, 0f);
                }
            }
        }

        // ----- controller / legacy input --------------------------------
        private static void PollController()
        {
            if (!Plugin.GamepadNavigation.Value)
            {
                // Keyboard is handled in Tick. Do not poll legacy joystick
                // axes/buttons either, so virtual pads cannot toggle the
                // overlay when controller navigation is off.
                return;
            }

            EnsureInput();

            if (_input != null)
            {
                try
                {
                    int pads = _input.getPadCount();
                    int index = InputAdapter.UI_INPUT_RECEIVE_PAD_INDEX;
                    InputAdapter.PAD_TYPE type = InputAdapter.PAD_TYPE.INVALID;
                    if (index >= 0 && index < pads)
                        type = _input.getControllerType(index);
                    if (index < 0 || index >= pads ||
                        type == InputAdapter.PAD_TYPE.INVALID ||
                        !_input.isConnected(index))
                    {
                        index = -1;
                        for (int i = pads - 1; i >= 0; i--)
                        {
                            if (!_input.isConnected(i)) continue;
                            var candidate = _input.getControllerType(i);
                            if (candidate == InputAdapter.PAD_TYPE.INVALID)
                                continue;
                            index = i;
                            type = candidate;
                            break;
                        }
                    }
                    if (!_padLogged)
                    {
                        _padLogged = true;
                        Plugin.Logger.LogInfo("Overlay input: pads=" + pads +
                            " padIndex=" + index + " type=" + type);
                    }
                    if (index >= 0)
                    {
                        _padIndex = index;
                        if (PollPad()) return;
                    }
                }
                catch (Exception e)
                {
                    if (!_padSeen)
                    {
                        _padSeen = true;
                        Plugin.Logger.LogWarning("Overlay pad poll: " + e.Message);
                    }
                }
            }

            PollKeyboardAxes();
        }

        private static bool PollPad()
        {
            if (Time.unscaledTime < _padIgnoreUntil)
            {
                SyncPadState();
                return false;
            }
            bool handled = false;

            bool tabL = PadEvent(InputAdapter.EVENT.UI_TAB_L);
            bool tabR = PadEvent(InputAdapter.EVENT.UI_TAB_R);
            if (Edge(ref _prevTabL, tabL))
            {
                SwitchTab(-1);
                handled = true;
            }
            if (Edge(ref _prevTabR, tabR))
            {
                SwitchTab(1);
                handled = true;
            }

            bool up = PadEvent(InputAdapter.EVENT.UI_UP);
            bool down = PadEvent(InputAdapter.EVENT.UI_DOWN);
            bool left = PadEvent(InputAdapter.EVENT.UI_LEFT);
            bool right = PadEvent(InputAdapter.EVENT.UI_RIGHT);
            bool submit = PadEvent(InputAdapter.EVENT.UI_SUBMIT);

            if (Edge(ref _prevUp, up))
            {
                if (AllowAction("up")) Move(-1);
                handled = true;
            }
            if (Edge(ref _prevDown, down))
            {
                if (AllowAction("down")) Move(1);
                handled = true;
            }
            if (Edge(ref _prevLeft, left))
            {
                if (AllowAction("left")) Adjust(-1);
                handled = true;
            }
            if (Edge(ref _prevRight, right))
            {
                if (AllowAction("right")) Adjust(1);
                handled = true;
            }
            if (Edge(ref _prevSubmit, submit))
            {
                if (AllowAction("submit")) Adjust(1);
                handled = true;
            }
            return handled;
        }

        private static void SyncPadState()
        {
            if (_input == null || _padIndex < 0) return;
            try
            {
                _prevTabL = PadEvent(InputAdapter.EVENT.UI_TAB_L);
                _prevTabR = PadEvent(InputAdapter.EVENT.UI_TAB_R);
                _prevUp = PadEvent(InputAdapter.EVENT.UI_UP);
                _prevDown = PadEvent(InputAdapter.EVENT.UI_DOWN);
                _prevLeft = PadEvent(InputAdapter.EVENT.UI_LEFT);
                _prevRight = PadEvent(InputAdapter.EVENT.UI_RIGHT);
                _prevSubmit = PadEvent(InputAdapter.EVENT.UI_SUBMIT);
            }
            catch
            {
            }
        }

        private static void PollKeyboardAxes()
        {
            if (!_axesChecked)
            {
                _axesChecked = true;
                try
                {
                    Input.GetAxisRaw("Vertical");
                    Input.GetAxisRaw("Horizontal");
                    _axesOk = true;
                    try
                    {
                        Input.GetAxisRaw("JoyAxis7");
                        Input.GetAxisRaw("JoyAxis6");
                        _dpadOk = true;
                    }
                    catch
                    {
                        _dpadOk = false;
                    }
                }
                catch
                {
                    _axesOk = false;
                }
            }

            float now = Time.unscaledTime;
            float vertical = 0f;
            float horizontal = 0f;
            if (_axesOk)
            {
                vertical = Input.GetAxisRaw("Vertical");
                horizontal = Input.GetAxisRaw("Horizontal");
            }
            if (_dpadOk)
            {
                float dpad = Input.GetAxisRaw("JoyAxis7");
                if (Mathf.Abs(dpad) > 0.55f) vertical = -dpad;
                dpad = Input.GetAxisRaw("JoyAxis6");
                if (Mathf.Abs(dpad) > 0.55f) horizontal = dpad;
            }
            if (_axesOk && now >= _nextVertical)
            {
                if (vertical > 0.55f)
                {
                    _nextVertical = now + 0.22f;
                    if (AllowAction("up")) Move(-1);
                }
                else if (vertical < -0.55f)
                {
                    _nextVertical = now + 0.22f;
                    if (AllowAction("down")) Move(1);
                }
            }
            if (_axesOk && now >= _nextHorizontal)
            {
                if (horizontal > 0.55f)
                {
                    _nextHorizontal = now + 0.22f;
                    if (AllowAction("right")) Adjust(1);
                }
                else if (horizontal < -0.55f)
                {
                    _nextHorizontal = now + 0.22f;
                    if (AllowAction("left")) Adjust(-1);
                }
            }

            if (Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                if (AllowAction("submit")) Adjust(1);
            }
            if (Input.GetKeyDown(KeyCode.JoystickButton1)) Toggle();
            if (Input.GetKeyDown(KeyCode.JoystickButton6)) Toggle();
            if (Input.GetKeyDown(KeyCode.JoystickButton7)) Toggle();

            if (ButtonDown("Submit"))
            {
                if (AllowAction("submit")) Adjust(1);
            }
            if (ButtonDown("Cancel")) Toggle();
        }

        private static bool ButtonDown(string name)
        {
            if (name == "Submit")
            {
                if (!_submitChecked)
                {
                    _submitChecked = true;
                    try { Input.GetButtonDown(name); _submitOk = true; }
                    catch { _submitOk = false; }
                }
                return _submitOk && Input.GetButtonDown(name);
            }
            if (name == "Cancel")
            {
                if (!_cancelChecked)
                {
                    _cancelChecked = true;
                    try { Input.GetButtonDown(name); _cancelOk = true; }
                    catch { _cancelOk = false; }
                }
                return _cancelOk && Input.GetButtonDown(name);
            }
            return false;
        }

        internal static bool ReplacePrefix()
        {
            if (!Plugin.ReplaceOptionsMenu.Value) return true;
            if (Time.unscaledTime < BypassUntil)
            {
                BypassUntil = 0f;
                return true;
            }
            if (Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift))
                return true;
            Toggle();
            return false;
        }
    }

    [HarmonyPatch(typeof(MainMenuUI), "onSubmitOther")]
    internal static class ReplaceOptionsGearPatch
    {
        private static bool Prefix()
        {
            return SettingsOverlay.ReplacePrefix();
        }
    }

    [HarmonyPatch(typeof(OtherUI), "onSubmitBasicOptionPanelButton")]
    internal static class ReplaceOtherBasicOptionPatch
    {
        private static bool Prefix()
        {
            return SettingsOverlay.ReplacePrefix();
        }
    }

    [HarmonyPatch(typeof(MainMenuUI), "onSubmitBasicOption")]
    internal static class ReplaceBasicOptionPatch
    {
        private static bool Prefix()
        {
            return SettingsOverlay.ReplacePrefix();
        }
    }
}
