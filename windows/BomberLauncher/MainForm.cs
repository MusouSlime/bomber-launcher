using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace BomberLauncher;

public class MainForm : Form
{
    private const string AppName = "Bomber Launcher";
    private static readonly Color Dark = Color.FromArgb(16, 26, 46);
    private static readonly Color Gold = Color.FromArgb(255, 204, 0);
    private static readonly Color Card = Color.White;
    private static readonly Color Ink = Color.FromArgb(22, 32, 46);
    private static readonly Color Muted = Color.FromArgb(92, 102, 116);

    private sealed record LangItem(string Name, byte Value)
    {
        public override string ToString() => Name;
    }

    private sealed record PadItem(string Name, int Value)
    {
        public override string ToString() => Name;
    }

    private sealed record AspectItem(string Name, double Ratio)
    {
        public override string ToString() => Name;
    }

    private static readonly AspectItem[] Aspects =
    {
        new("Native / display", 0),
        new("16:9", 16.0 / 9.0),
        new("16:10", 16.0 / 10.0),
        new("21:9", 21.0 / 9.0),
        new("4:3", 4.0 / 3.0),
    };

    private static readonly PadItem[] Pads =
    {
        new("Disabled", -1), new("A / Cross", 0), new("B / Circle", 1),
        new("X / Square", 2), new("Y / Triangle", 3), new("LB / L1", 4),
        new("RB / R1", 5), new("Back / Share", 6), new("Start / Options", 7),
    };

    private static readonly LangItem[] Languages =
    {
        new("Auto (Steam)", 255), new("English", 1), new("Japanese", 0),
        new("French", 2), new("German", 3), new("Italian", 4),
        new("Spanish", 5), new("Dutch", 8), new("Portuguese", 9),
        new("Russian", 10), new("Korean", 7),
        new("Chinese (Simplified)", 6), new("Chinese (Traditional)", 11),
    };

    private readonly FlowLayoutPanel _flow;
    private readonly Label _status;
    private readonly Button _play;
    private readonly ComboBox _language = new();
    private readonly CheckBox _borderless = new();
    private readonly ComboBox _aspectCombo = new();
    private readonly CheckBox _dual = new();
    private readonly CheckBox _intro = new();
    private readonly CheckBox _skipMovies = new();
    private readonly ComboBox _padCombo = new();
    private readonly CheckBox _logos = new();
    private readonly CheckBox _autoSkip = new();
    private readonly CheckBox _master = new();
    private readonly CheckBox _softReset = new();
    private readonly CheckBox _fastResults = new();
    private readonly CheckBox _motionBlur = new();
    private readonly CheckBox _dof = new();
    private readonly CheckBox _bloom = new();

    private string? _game;
    private bool _loading;

    public MainForm()
    {
        Text = AppName;
        ClientSize = new Size(740, 900);
        MinimumSize = new Size(680, 700);
        BackColor = Color.FromArgb(238, 243, 248);
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new HeaderPanel { Dock = DockStyle.Top, Height = 168 };
        Controls.Add(header);

        var title = new Label
        {
            Text = "SUPER BOMBERMAN R",
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            ForeColor = Gold,
            AutoSize = true,
            Location = new Point(24, 26),
            BackColor = Color.Transparent,
        };
        var sub = new Label
        {
            Text = "B O M B E R   L A U N C H E R",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(220, 235, 255),
            AutoSize = true,
            Location = new Point(27, 74),
            BackColor = Color.Transparent,
        };
        _play = new Button
        {
            Text = "PLAY",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            BackColor = Gold,
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(132, 46),
            Location = new Point(24, 102),
            Cursor = Cursors.Hand,
        };
        _play.FlatAppearance.BorderSize = 0;
        _play.Click += (_, _) => Launch();

        _status = new Label
        {
            Text = "READY",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(31, 157, 85),
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Location = new Point(170, 112),
        };

        header.Controls.AddRange(new Control[] { title, sub, _play, _status });
        TryLoadHero(header);

        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16, 12, 16, 16),
            BackColor = BackColor,
        };
        Controls.Add(_flow);
        _flow.BringToFront();
        _flow.Resize += (_, _) => LayoutCards();

        _language.DropDownStyle = ComboBoxStyle.DropDownList;
        _language.Width = 230;
        foreach (var l in Languages) _language.Items.Add(l);
        _language.SelectedIndexChanged += (_, _) => ChangeLanguage();

        AddSection("CHANGE LANGUAGE");
        var lang = AddCard();
        AddRow(lang, "Text Language",
            "Writes the language byte in savedata.bin", _language);

        AddSection("BASIC SETTINGS");
        var basic = AddCard();
        AddRow(basic, "Borderless Fullscreen",
            "Launch with -popupwindow at desktop resolution", _borderless);
        Separator(basic);
        _aspectCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _aspectCombo.Width = 230;
        foreach (var a in Aspects) _aspectCombo.Items.Add(a);
        _aspectCombo.SelectedIndex = 0;
        AddRow(basic, "Aspect Ratio",
            "Window shape; the game fits inside the desktop with bars",
            _aspectCombo);
        Separator(basic);
        AddButtonRow(basic, "Custom BGM Folder",
            "Folder of .ogg/.wav/.mp3 tracks; empty keeps original BGM",
            "Choose", ChooseBgmFolder);
        Separator(basic);
        AddRow(basic, "Japanese Voices",
            "JP voice acting with the selected text language", _dual);
        Separator(basic);
        AddRow(basic, "Skip Boot Intro",
            "Replaces the ~4 minute opening movie", _intro);
        Separator(basic);
        AddRow(basic, "Skip Cutscenes",
            "Space, A/Cross or B/Circle (BepInEx, next launch)", _skipMovies);
        Separator(basic);
        _padCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _padCombo.Width = 230;
        foreach (var p in Pads) _padCombo.Items.Add(p);
        AddRow(basic, "Controller Skip Button",
            "Primary pad button; B is also accepted", _padCombo);
        Separator(basic);
        AddRow(basic, "Remove Publisher Logos",
            "Konami / 33rd / HexaDrive logo states", _logos);
        Separator(basic);
        AddRow(basic, "Auto-skip Cutscenes",
            "Skips every movie after 5 seconds", _autoSkip);
        Separator(basic);
        AddRow(basic, "Master Difficulty",
            "Adds the hidden Master option to the difficulty menu", _master);
        Separator(basic);
        AddRow(basic, "Soft Reset Combo",
            "Hold LB + RB + Start for 0.75s to save and return to title", _softReset);

        AddSection("ENVIRONMENT");
        var env = AddCard();
        AddRow(env, "Motion Blur",
            "Post-processing effect (BepInEx, next launch)", _motionBlur);
        Separator(env);
        AddRow(env, "Depth of Field",
            "Post-processing effect (BepInEx, next launch)", _dof);
        Separator(env);
        AddRow(env, "Bloom",
            "Post-processing effect (BepInEx, next launch)", _bloom);
        Separator(env);
        AddRow(env, "Faster Result Screens",
            "Halves post-battle result delays (BepInEx)", _fastResults);

        AddSection("OTHER");
        var other = AddCard();
        AddButtonRow(other, "Install BepInEx + Plugin",
            "Downloads BepInEx x86 and installs BomberQoL", "Install",
            InstallBepInEx);
        Separator(other);
        AddButtonRow(other, "Restore Original Files",
            "Reverts dual audio and intro changes", "Restore", RestoreAll);
        Separator(other);
        AddButtonRow(other, "Back Up Save",
            "Timestamped copy under LocalAppData", "Back up", BackupSave);
        Separator(other);
        AddButtonRow(other, "Open Backup Folder",
            Core.BackupRoot, "Open", OpenBackups);

        var footer = new Label
        {
            Text = "Unofficial quality-of-life launcher. Super Bomberman R is © Konami.",
            ForeColor = Muted,
            AutoSize = true,
            Margin = new Padding(4, 10, 0, 0),
        };
        _flow.Controls.Add(footer);

        _borderless.Checked = true;
        _borderless.CheckedChanged += (_, _) => { };
        _dual.CheckedChanged += (_, _) => ToggleDual();
        _intro.CheckedChanged += (_, _) => ToggleIntro();
        _skipMovies.CheckedChanged += (_, _) => SavePlugin("Movies", "SkipEnabled", _skipMovies.Checked);
        _motionBlur.CheckedChanged += (_, _) => SavePlugin("Graphics", "DisableMotionBlur", !_motionBlur.Checked);
        _dof.CheckedChanged += (_, _) => SavePlugin("Graphics", "DisableDepthOfField", !_dof.Checked);
        _bloom.CheckedChanged += (_, _) => SavePlugin("Graphics", "DisableBloom", !_bloom.Checked);
        _padCombo.SelectedIndexChanged += (_, _) => SavePadButton();
        _logos.CheckedChanged += (_, _) => SavePlugin("Movies", "AutoSkipLogos", _logos.Checked);
        _autoSkip.CheckedChanged += (_, _) => SavePluginValue("Movies", "AutoSkipMovies", _autoSkip.Checked ? "5" : "0");
        _master.CheckedChanged += (_, _) => SavePlugin("Gameplay", "UnlockMaster", _master.Checked);
        _softReset.CheckedChanged += (_, _) => SavePlugin("Gameplay", "SoftResetEnabled", _softReset.Checked);
        _fastResults.CheckedChanged += (_, _) => SavePlugin("Results", "FastResults", _fastResults.Checked);

        Shown += (_, _) => RefreshState();
    }

    private void TryLoadHero(Control header)
    {
        string p = Path.Combine(AppContext.BaseDirectory, "assets", "hero.png");
        if (!File.Exists(p)) return;
        try
        {
            var img = Image.FromFile(p);
            header.BackgroundImage = img;
            header.BackgroundImageLayout = ImageLayout.Stretch;
            foreach (Control c in header.Controls) c.BackColor = Color.Transparent;
        }
        catch { }
    }

    private sealed class HeaderPanel : Panel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            if (BackgroundImage != null) { base.OnPaint(e); return; }
            using var b = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(22, 35, 61), Color.FromArgb(13, 21, 38), 25f);
            e.Graphics.FillRectangle(b, ClientRectangle);
        }
    }

    private void AddSection(string text)
    {
        var bar = new Panel
        {
            Height = 34,
            BackColor = Color.FromArgb(60, 70, 71),
            Margin = new Padding(0, 8, 0, 6),
        };
        var label = new Label
        {
            Text = text,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
        };
        bar.Controls.Add(label);
        _flow.Controls.Add(bar);
    }

    private TableLayoutPanel AddCard()
    {
        var card = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Card,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 0, 0, 8),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _flow.Controls.Add(card);
        return card;
    }

    private void AddRow(TableLayoutPanel card, string title, string subtitle,
                        Control control)
    {
        var text = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Dock = DockStyle.Fill,
        };
        text.Controls.Add(new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Ink,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        });
        text.Controls.Add(new Label
        {
            Text = subtitle,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Muted,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0),
        });
        int row = card.RowCount++;
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.Controls.Add(text, 0, row);

        control.Anchor = AnchorStyles.Right;
        control.Margin = new Padding(10, 12, 0, 12);
        card.Controls.Add(control, 1, row);
    }

    private void AddButtonRow(TableLayoutPanel card, string title,
                              string subtitle, string linkText, Action action)
    {
        var link = new LinkLabel
        {
            Text = linkText,
            AutoSize = true,
            LinkColor = Color.FromArgb(30, 90, 160),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(10, 14, 0, 12),
        };
        link.LinkClicked += (_, _) => action();
        AddRow(card, title, subtitle, link);
    }

    private void Separator(TableLayoutPanel card)
    {
        int row = card.RowCount++;
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var sep = new Panel
        {
            Height = 1,
            BackColor = Color.FromArgb(225, 229, 234),
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        card.Controls.Add(sep, 0, row);
        card.SetColumnSpan(sep, 2);
    }

    private void LayoutCards()
    {
        int width = Math.Max(320, _flow.ClientSize.Width - 48);
        foreach (Control c in _flow.Controls)
        {
            c.Width = width;
            if (c is TableLayoutPanel t)
                t.MinimumSize = new Size(width, 0);
        }
    }

    // ---- state ----------------------------------------------------------
    private void RefreshState()
    {
        _loading = true;
        try
        {
            _game = Core.FindGame();
            bool running = Core.GameRunning();
            bool plugin = _game != null && Core.PluginInstalled(_game);

            if (_game == null)
            {
                SetStatus("GAME NOT FOUND", Color.FromArgb(185, 28, 28));
            }
            else if (running)
            {
                SetStatus("RUNNING", Color.FromArgb(194, 106, 0));
            }
            else
            {
                SetStatus("READY", Color.FromArgb(31, 157, 85));
            }

            bool editable = _game != null && !running;
            _play.Enabled = _game != null;
            _language.Enabled = editable;
            _language.SelectedItem = Languages.First(l => l.Value == 255);
            _borderless.Enabled = _game != null;
            _aspectCombo.Enabled = _game != null;
            _dual.Enabled = editable;
            _intro.Enabled = editable;
            _skipMovies.Enabled = editable && plugin;
            _padCombo.Enabled = editable && plugin;
            _logos.Enabled = editable && plugin;
            _autoSkip.Enabled = editable && plugin;
            _master.Enabled = editable && plugin;
            _fastResults.Enabled = editable && plugin;
            _motionBlur.Enabled = editable && plugin;
            _dof.Enabled = editable && plugin;
            _bloom.Enabled = editable && plugin;

            _dual.Checked = false;
            _intro.Checked = false;
            _skipMovies.Checked = true;
            _logos.Checked = true;
            _autoSkip.Checked = false;
            _master.Checked = false;
            _fastResults.Checked = false;
            _padCombo.SelectedIndex = 1;
            _motionBlur.Checked = false;
            _dof.Checked = false;
            _bloom.Checked = true;

            if (_game != null)
            {
                try { _dual.Checked = Core.DualAudio(_game); } catch { }
                try { _intro.Checked = Core.IntroSkipped(_game) == true; } catch { }
                try
                {
                    byte lang = Core.ReadLanguage(_game);
                    var item = Languages.FirstOrDefault(l => l.Value == lang);
                    if (item != null) _language.SelectedItem = item;
                }
                catch { }
                if (plugin)
                {
                    _skipMovies.Checked =
                        Core.GetPluginOption(_game, "Movies", "SkipEnabled", "true") == "true";
                    _motionBlur.Checked =
                        Core.GetPluginOption(_game, "Graphics", "DisableMotionBlur", "true") != "true";
                    _dof.Checked =
                        Core.GetPluginOption(_game, "Graphics", "DisableDepthOfField", "true") != "true";
                    _bloom.Checked =
                        Core.GetPluginOption(_game, "Graphics", "DisableBloom", "false") != "true";
                    _logos.Checked =
                        Core.GetPluginOption(_game, "Movies", "AutoSkipLogos", "true") == "true";
                    _autoSkip.Checked =
                        float.TryParse(Core.GetPluginOption(_game, "Movies", "AutoSkipMovies", "0"), out float asec) && asec > 0;
                    _fastResults.Checked =
                        Core.GetPluginOption(_game, "Results", "FastResults", "false") == "true";
                    _master.Checked =
                        Core.GetPluginOption(_game, "Gameplay", "UnlockMaster", "false") == "true";
                    _softReset.Checked =
                        Core.GetPluginOption(_game, "Gameplay", "SoftResetEnabled", "true") == "true";
                    if (int.TryParse(Core.GetPluginOption(_game, "Movies", "SkipJoystickButton", "0"), out int pad))
                    {
                        int idx = Array.FindIndex(Pads, p => p.Value == pad);
                        if (idx >= 0) _padCombo.SelectedIndex = idx;
                    }
                }
            }
        }
        finally
        {
            _loading = false;
            LayoutCards();
        }
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.BackColor = color;
    }

    private bool NeedGame()
    {
        if (_game != null) return true;
        MessageBox.Show(this, "Super Bomberman R was not found.", AppName);
        return false;
    }

    private bool NeedClosed()
    {
        if (!Core.GameRunning()) return true;
        MessageBox.Show(this, "Close the game first.", AppName);
        RefreshState();
        return false;
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        RefreshState();
    }

    // ---- handlers -------------------------------------------------------
    private void Launch()
    {
        if (!NeedGame()) return;
        double ratio = Aspects[Math.Max(0, _aspectCombo.SelectedIndex)].Ratio;
        try { Core.Launch(_game!, _borderless.Checked, ratio); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ChangeLanguage()
    {
        if (_loading || _game == null || !NeedClosed()) return;
        if (_language.SelectedItem is not LangItem item) return;
        Guard(() => Core.SetLanguage(_game!, item.Value));
    }

    private void ToggleDual()
    {
        if (_loading || _game == null || !NeedClosed()) return;
        Guard(() => Core.SetDualAudio(_game!, _dual.Checked));
    }

    private void ToggleIntro()
    {
        if (_loading || _game == null || !NeedClosed()) return;
        Guard(() => Core.SetIntroSkip(_game!, _intro.Checked));
    }

    private void ChooseBgmFolder()
    {
        if (_game == null || !Core.PluginInstalled(_game)) return;
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select a folder of .ogg/.wav/.mp3 BGM tracks",
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            Guard(() => Core.SetPluginValue(_game!, "Audio",
                "CustomBgmFolder", dlg.SelectedPath));
    }

    private void SavePlugin(string section, string key, bool value)
    {
        if (_loading || _game == null) return;
        if (!Core.PluginInstalled(_game))
        {
            MessageBox.Show(this,
                "BomberQoL plugin not installed. Run install-bepinex.sh from the repo.",
                AppName);
            RefreshState();
            return;
        }
        Guard(() => Core.SetPluginOption(_game!, section, key, value));
    }

    private void SavePadButton()
    {
        if (_loading || _game == null) return;
        if (!Core.PluginInstalled(_game)) return;
        if (_padCombo.SelectedItem is not PadItem pad) return;
        Guard(() => Core.SetPluginValue(_game!, "Movies",
            "SkipJoystickButton", pad.Value.ToString()));
    }

    private void SavePluginValue(string section, string key, string value)
    {
        if (_loading || _game == null) return;
        if (!Core.PluginInstalled(_game)) return;
        Guard(() => Core.SetPluginValue(_game!, section, key, value));
    }

    private void InstallBepInEx()
    {
        if (_game == null || !NeedClosed()) return;
        if (MessageBox.Show(this,
                "Download BepInEx x86 and install BomberQoL?",
                AppName, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;
        Guard(() =>
        {
            Core.InstallBepInEx(_game!);
            MessageBox.Show(this, "BepInEx + BomberQoL installed. Restart the game.",
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void RestoreAll()
    {
        if (_game == null || !NeedClosed()) return;
        if (MessageBox.Show(this,
                "Restore the original DLL and boot movies?",
                AppName, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;
        Guard(() => Core.RestoreAll(_game!));
    }

    private void BackupSave()
    {
        if (_game == null) return;
        Guard(() => Core.BackupSave(_game!));
    }

    private void OpenBackups()
    {
        Directory.CreateDirectory(Core.BackupRoot);
        Process.Start(new ProcessStartInfo("explorer.exe", Core.BackupRoot)
        {
            UseShellExecute = true
        });
    }
}
