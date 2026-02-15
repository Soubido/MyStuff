using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.UI;
using NewRhinoGold.Core;
using NewRhinoGold.Helpers;
using NewRhinoGold.BezelStudio;

namespace NewRhinoGold.Studio
{
    public class HeadStudioDlg : Form
    {
        private readonly GemDisplayCond _previewConduit;
        private Guid _editingObjectId = Guid.Empty;
        private List<NewRhinoGold.Helpers.GemData> _selectedGems = new List<NewRhinoGold.Helpers.GemData>();

        private readonly Eto.Drawing.Font _fontStandard = Eto.Drawing.Fonts.Sans(10);
        private readonly Eto.Drawing.Font _fontInput = Eto.Drawing.Fonts.Sans(11);
        private readonly Eto.Drawing.Font _fontHeader = Eto.Drawing.Fonts.Sans(12, FontStyle.Bold);
        private readonly Eto.Drawing.Font _fontLabelBold = Eto.Drawing.Fonts.Sans(10, FontStyle.Bold);

        private Button _btnSelectGem;
        private TabControl _tabControl;
        private Button _btnBuild, _btnClose;

        // General
        private NumericStepper _numGemInside, _numProngCount;
        private CheckBox _chkLockDia;
        private NumericStepper _numTopDia, _numMidDia, _numBotDia;
        private NumericStepper _numTopOff, _numMidOff, _numBotOff;
        private NumericStepper _numDepthBelow, _numHeight;

        // Material
        private DropDown _drpMetal;
        private TextBox _txtWeight;
        private System.Drawing.Color _headColor = System.Drawing.Color.Silver;

        // Profile
        private GridView _gridProfiles;
        private Button _btnAddProfile;
        private NumericStepper _numTopRot, _numMidRot, _numBotRot;

        // Rails — GridView statt DropDown für Profilauswahl
        private CheckBox _chkTopRail;
        private GridView _gridTopRailProfile;
        private NumericStepper _numTopRailWidth, _numTopRailHeight, _numTopRailPos, _numTopRailOffset, _numTopRailRot;

        private CheckBox _chkBotRail;
        private GridView _gridBotRailProfile;
        private NumericStepper _numBotRailWidth, _numBotRailHeight, _numBotRailPos, _numBotRailOffset, _numBotRailRot;

        // Sort
        private CheckBox _chkMoveAll;
        private Scrollable _scrollSort;
        private StackLayout _sortLayout;
        private List<Slider> _prongSliders = new List<Slider>();
        private List<double> _currentProngPositions = new List<double>();
        private List<NumericStepper> _prongRotationSteppers = new List<NumericStepper>();
        private List<double> _currentProngRotations = new List<double>();

        private bool _suspendUpdates = true;

        // Breiten
        private const int CompactW = 75;
        private const int RailStepperW = 60;
        private const int MetalDrpW = 180;

        public HeadStudioDlg()
        {
            _suspendUpdates = true;
            Title = "Head Studio";
            ClientSize = new Size(340, 530);
            Topmost = true;
            Resizable = false;
            _previewConduit = new GemDisplayCond();

            Content = BuildMainLayout();

            Shown += (s, e) => {
                _previewConduit.Enable();
                if (_numProngCount != null) RebuildSortList((int)_numProngCount.Value);
                _suspendUpdates = false;
                UpdatePreview();
            };

            Closed += (s, e) => {
                _previewConduit.Disable();
                RhinoDoc.ActiveDoc?.Views.Redraw();
            };
        }

        // --- Stepper Factories ---
        private NumericStepper MakeStepper(double v, int d, double inc, int w)
        {
            var s = new NumericStepper { Value = v, DecimalPlaces = d, Increment = inc, Font = _fontInput, Height = 24, Width = w };
            s.ValueChanged += (sender, e) => UpdatePreview();
            return s;
        }
        private NumericStepper CompactStepper(double v, int d = 2, double inc = 0.1) => MakeStepper(v, d, inc, CompactW);
        private NumericStepper RailStepper(double v, int d = 2, double inc = 0.1) => MakeStepper(v, d, inc, RailStepperW);

        private Label H(string text) => new Label { Text = text, Font = _fontHeader, TextColor = Colors.Gray };
        private Label L(string text) => new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, Font = _fontStandard };

        // --- Profil-GridView Factory (identisch für Profile-Tab und Rails) ---
        private GridView CreateProfileGridView(int height)
        {
            var gv = new GridView { Height = height, AllowMultipleSelection = false };
            gv.Columns.Add(new GridColumn
            {
                HeaderText = "Profile",
                Width = 180,
                DataCell = new ImageTextCell
                {
                    ImageBinding = Binding.Property<HeadProfileItem, Image>(x => x.Preview),
                    TextBinding = Binding.Property<HeadProfileItem, string>(x => x.Name)
                }
            });
            gv.DataStore = HeadProfileLibrary.GetProfileItems();
            gv.SelectionChanged += (s, e) => UpdatePreview();

            var list = (List<HeadProfileItem>)gv.DataStore;
            if (list != null && list.Count > 0) gv.SelectRow(0);
            return gv;
        }

        private void SelectProfileByName(GridView gv, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var list = gv.DataStore as List<HeadProfileItem>;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Name == name) { gv.SelectRow(i); return; }
        }

        private string GetSelectedProfileName(GridView gv)
        {
            return (gv.SelectedItem as HeadProfileItem)?.Name;
        }

        // ===============================================================
        // MAIN LAYOUT
        // ===============================================================

        private Control BuildMainLayout()
        {
            _btnSelectGem = new Button { Text = "Select Gems (Multiple)", Font = _fontStandard, Height = 30 };
            _btnSelectGem.Click += OnSelectGem;

            _tabControl = new TabControl();
            _tabControl.Pages.Add(new TabPage { Text = "General", Content = BuildGeneralTab() });
            _tabControl.Pages.Add(new TabPage { Text = "Profile", Content = BuildProngProfileTab() });
            _tabControl.Pages.Add(new TabPage { Text = "Rails", Content = BuildAuflageTab() });
            _tabControl.Pages.Add(new TabPage { Text = "Sort", Content = BuildSortTab() });

            _btnBuild = new Button { Text = "Build", Font = _fontStandard, Height = 30 };
            _btnBuild.Click += OnBuild;
            _btnClose = new Button { Text = "Cancel", Font = _fontStandard, Height = 30 };
            _btnClose.Click += (s, e) => Close();

            var layout = new DynamicLayout { Padding = new Padding(10), Spacing = new Size(5, 5) };
            layout.BeginVertical();
            layout.AddRow(_btnSelectGem);
            layout.AddRow(null);
            layout.AddRow(_tabControl);
            layout.Add(null);
            layout.AddRow(new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows = { new TableRow(new TableCell(_btnBuild, true), new TableCell(_btnClose, true)) }
            });
            layout.EndVertical();
            return layout;
        }

        // ===============================================================
        // GENERAL TAB
        // ===============================================================

        private Control BuildGeneralTab()
        {
            _numProngCount = CompactStepper(4, 0, 1.0);
            _numProngCount.ValueChanged += (s, e) => {
                if (!_suspendUpdates) RebuildSortList((int)_numProngCount.Value);
                UpdatePreview();
            };
            _numGemInside = CompactStepper(30, 0, 1.0);

            _numTopDia = CompactStepper(1.0); _numMidDia = CompactStepper(1.0); _numBotDia = CompactStepper(1.0);
            _chkLockDia = new CheckBox { Text = "Lock", Checked = true, ToolTip = "Synchronize", Font = _fontStandard };

            void SyncDia(double val)
            {
                if (_chkLockDia.Checked == true && !_suspendUpdates)
                {
                    bool old = _suspendUpdates; _suspendUpdates = true;
                    _numTopDia.Value = val; _numMidDia.Value = val; _numBotDia.Value = val;
                    _suspendUpdates = old; UpdatePreview();
                }
                else UpdatePreview();
            }
            _numTopDia.ValueChanged += (s, e) => SyncDia(_numTopDia.Value);
            _numMidDia.ValueChanged += (s, e) => SyncDia(_numMidDia.Value);
            _numBotDia.ValueChanged += (s, e) => SyncDia(_numBotDia.Value);

            _numTopOff = CompactStepper(0.0); _numMidOff = CompactStepper(0.0); _numBotOff = CompactStepper(0.0);
            _numDepthBelow = CompactStepper(2.0); _numHeight = CompactStepper(5.0);

            // Material DropDown — 75% Breite
            _drpMetal = new DropDown { Font = _fontStandard, Height = 26, Width = MetalDrpW };
            foreach (var metal in Densities.Metals)
                _drpMetal.Items.Add(new ListItem { Text = metal.Name, Key = metal.Id });
            if (_drpMetal.Items.Count > 0) _drpMetal.SelectedIndex = 0;
            _drpMetal.SelectedIndexChanged += OnMetalChanged;
            ApplyMetalColor();

            _txtWeight = new TextBox { ReadOnly = true, Text = "0.000 g", Font = _fontStandard, Height = 24, Width = CompactW };

            // Layout
            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 6) };
            layout.BeginVertical();

            layout.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Prong Count:"), false), new TableCell(_numProngCount, false)) } });
            layout.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Gem Inside (%):"), false), new TableCell(_numGemInside, false)) } });
            layout.AddRow(null);

            // Diameter Grid
            layout.AddRow(new TableLayout
            {
                Spacing = new Size(4, 0),
                Rows = { new TableRow(
                    new TableCell(new Label { Text = "", Width = 30 }, false),
                    new TableCell(new Label { Text = "Diameter", Font = _fontLabelBold }, false),
                    new TableCell(_chkLockDia, false),
                    new TableCell(new Label { Text = "Shift", Font = _fontLabelBold }, false)) }
            });

            TableRow DiaRow(string lbl, NumericStepper dia, NumericStepper off) =>
                new TableRow(
                    new TableCell(new Label { Text = lbl, VerticalAlignment = VerticalAlignment.Center, Font = _fontStandard, Width = 30 }, false),
                    new TableCell(dia, false), new TableCell(new Panel { Width = 6 }, false), new TableCell(off, false));

            var dg = new TableLayout { Spacing = new Size(4, 4) };
            dg.Rows.Add(DiaRow("Top:", _numTopDia, _numTopOff));
            dg.Rows.Add(DiaRow("Mid:", _numMidDia, _numMidOff));
            dg.Rows.Add(DiaRow("Bot:", _numBotDia, _numBotOff));
            layout.AddRow(dg);
            layout.AddRow(null);

            layout.AddRow(H("Dimensions"));
            layout.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Depth below Gem:"), false), new TableCell(_numDepthBelow, false)) } });
            layout.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Total Height:"), false), new TableCell(_numHeight, false)) } });
            layout.AddRow(null);

            layout.AddRow(H("Material"));
            layout.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Metal:"), false), new TableCell(_drpMetal, false)) } });
            layout.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Weight:"), false), new TableCell(_txtWeight, false)) } });

            layout.EndVertical();
            return new Scrollable { Content = layout, Border = BorderType.None };
        }

        // ===============================================================
        // PROFILE TAB
        // ===============================================================

        private Control BuildProngProfileTab()
        {
            _gridProfiles = CreateProfileGridView(200);

            _btnAddProfile = new Button { Text = "Add Custom Curve", Font = _fontStandard };
            _btnAddProfile.Click += OnAddProfileFromRhino;

            _numTopRot = CompactStepper(0, 0, 5); _numTopRot.MinValue = 0; _numTopRot.MaxValue = 360;
            _numMidRot = CompactStepper(0, 0, 5); _numMidRot.MinValue = 0; _numMidRot.MaxValue = 360;
            _numBotRot = CompactStepper(0, 0, 5); _numBotRot.MinValue = 0; _numBotRot.MaxValue = 360;

            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };
            layout.BeginVertical();
            layout.AddRow(H("Select Profile"));
            layout.AddRow(_gridProfiles);
            layout.AddRow(_btnAddProfile);
            layout.AddRow(null);
            layout.AddRow(H("Profile Rotation (\u00b0)"));
            layout.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Top:"), false), new TableCell(_numTopRot, false)) } });
            layout.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Mid:"), false), new TableCell(_numMidRot, false)) } });
            layout.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Bot:"), false), new TableCell(_numBotRot, false)) } });
            layout.EndVertical();
            return new Scrollable { Content = layout, Border = BorderType.None };
        }

        // ===============================================================
        // RAILS TAB — GridView mit ImageTextCell (identisch zu Profile-Tab)
        // ===============================================================

        private Control BuildAuflageTab()
        {
            // Top Rail
            _chkTopRail = new CheckBox { Text = "Enable Top Rail", Checked = true, Font = _fontStandard };
            _chkTopRail.CheckedChanged += (s, e) => UpdatePreview();
            _gridTopRailProfile = CreateProfileGridView(80);
            _numTopRailWidth = RailStepper(0.8); _numTopRailHeight = RailStepper(0.8);
            _numTopRailPos = RailStepper(-0.5); _numTopRailOffset = RailStepper(0.0);
            _numTopRailRot = RailStepper(0, 0, 5);

            // Bottom Rail
            _chkBotRail = new CheckBox { Text = "Enable Bottom Rail", Checked = true, Font = _fontStandard };
            _chkBotRail.CheckedChanged += (s, e) => UpdatePreview();
            _gridBotRailProfile = CreateProfileGridView(80);
            _numBotRailWidth = RailStepper(0.8); _numBotRailHeight = RailStepper(0.8);
            _numBotRailPos = RailStepper(-2.0); _numBotRailOffset = RailStepper(0.0);
            _numBotRailRot = RailStepper(0, 0, 5);

            Control BuildRailGroup(string title, CheckBox enable, GridView profGrid,
                NumericStepper w, NumericStepper h, NumericStepper z, NumericStepper off, NumericStepper rot)
            {
                var gl = new DynamicLayout { Padding = 5, Spacing = new Size(5, 4) };
                gl.BeginVertical();
                gl.AddRow(enable);
                gl.AddRow(L("Profile:"));
                gl.AddRow(profGrid);

                var sizeRow = new TableLayout
                {
                    Spacing = new Size(4, 0),
                    Rows = { new TableRow(
                        new TableCell(w, false),
                        new TableCell(new Label { Text = "x", VerticalAlignment = VerticalAlignment.Center }, false),
                        new TableCell(h, false)) }
                };
                gl.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Size (WxH):"), false), new TableCell(sizeRow, false)) } });
                gl.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Z-Pos:"), false), new TableCell(z, false)) } });
                gl.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Offset:"), false), new TableCell(off, false)) } });
                gl.AddRow(new TableLayout { Spacing = new Size(0, 0), Rows = { new TableRow(new TableCell(L("Rotation:"), false), new TableCell(rot, false)) } });
                gl.EndVertical();
                return new GroupBox { Text = title, Content = gl, Font = _fontStandard };
            }

            var topGroup = BuildRailGroup("Top Rail", _chkTopRail, _gridTopRailProfile,
                _numTopRailWidth, _numTopRailHeight, _numTopRailPos, _numTopRailOffset, _numTopRailRot);
            var botGroup = BuildRailGroup("Bottom Rail", _chkBotRail, _gridBotRailProfile,
                _numBotRailWidth, _numBotRailHeight, _numBotRailPos, _numBotRailOffset, _numBotRailRot);

            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };
            layout.BeginVertical();
            layout.AddRow(topGroup);
            layout.AddRow(null);
            layout.AddRow(botGroup);
            layout.EndVertical();
            return new Scrollable { Content = layout, Border = BorderType.None };
        }

        // ===============================================================
        // SORT TAB
        // ===============================================================

        private Control BuildSortTab()
        {
            _chkMoveAll = new CheckBox { Text = "Move All together", Font = _fontStandard };
            _sortLayout = new StackLayout { Spacing = 5, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            _scrollSort = new Scrollable { Content = _sortLayout, Border = BorderType.Bezel, Height = 300 };
            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };
            layout.BeginVertical();
            layout.AddRow(_chkMoveAll);
            layout.AddRow(new Label { Text = "Prong Position (0-100%) & Rotation (\u00b0):", Font = _fontStandard });
            layout.AddRow(_scrollSort);
            layout.EndVertical();
            return layout;
        }

        // ===============================================================
        // MATERIAL
        // ===============================================================

        private void OnMetalChanged(object sender, EventArgs e) { ApplyMetalColor(); UpdatePreview(); }

        private void ApplyMetalColor()
        {
            string key = GetSelectedMetalKey();
            if (string.IsNullOrEmpty(key)) return;
            var info = Densities.Get(key);
            if (info != null) _headColor = info.DisplayColor;
        }

        private string GetSelectedMetalKey()
        {
            if (_drpMetal == null || _drpMetal.SelectedIndex < 0) return null;
            return (_drpMetal.Items[_drpMetal.SelectedIndex] as ListItem)?.Key;
        }

        private string GetSelectedMetalName()
        {
            if (_drpMetal == null || _drpMetal.SelectedIndex < 0) return null;
            return (_drpMetal.Items[_drpMetal.SelectedIndex] as ListItem)?.Text;
        }

        private void CalculateWeight(List<Brep> breps)
        {
            if (_txtWeight == null) return;
            string key = GetSelectedMetalKey();
            if (string.IsNullOrEmpty(key)) { _txtWeight.Text = "0.000 g"; return; }
            double density = Densities.GetDensity(key);
            double vol = 0.0;
            foreach (var b in breps)
            {
                if (b == null || !b.IsValid) continue;
                var mp = VolumeMassProperties.Compute(b);
                if (mp != null) vol += Math.Abs(mp.Volume);
            }
            _txtWeight.Text = $"{vol * (density / 1000.0):F3} g";
        }

        // ===============================================================
        // SORT
        // ===============================================================

        private void RebuildSortList(int count)
        {
            if (_sortLayout == null) return;
            bool old = _suspendUpdates; _suspendUpdates = true;
            _sortLayout.Items.Clear();
            _prongSliders.Clear(); _currentProngPositions.Clear();
            _prongRotationSteppers.Clear(); _currentProngRotations.Clear();

            for (int i = 0; i < count; i++)
            {
                double pos = (double)i / count;
                _currentProngPositions.Add(pos);
                _currentProngRotations.Add(0.0);

                var slider = new Slider { MinValue = 0, MaxValue = 100, Value = (int)(pos * 100), Tag = i };
                slider.ValueChanged += OnSortSliderChanged;
                _prongSliders.Add(slider);

                var rotStepper = new NumericStepper
                { Value = 0, DecimalPlaces = 0, MinValue = 0, MaxValue = 360, Increment = 5, Font = _fontInput, Height = 24, Width = 56, Tag = i };
                rotStepper.ValueChanged += OnProngRotationChanged;
                _prongRotationSteppers.Add(rotStepper);

                _sortLayout.Items.Add(new TableLayout
                {
                    Spacing = new Size(3, 0),
                    Rows = { new TableRow(
                        new TableCell(new Label { Text = $"P{i + 1}", Width = 24, Font = _fontStandard, VerticalAlignment = VerticalAlignment.Center }, false),
                        new TableCell(slider, true),
                        new TableCell(new Label { Text = "\u00b0", Width = 10, Font = _fontStandard, VerticalAlignment = VerticalAlignment.Center }, false),
                        new TableCell(rotStepper, false)) }
                });
            }
            _suspendUpdates = old;
        }

        private void OnProngRotationChanged(object sender, EventArgs e)
        {
            if (_suspendUpdates) return;
            var st = sender as NumericStepper; if (st == null) return;
            int idx = (int)st.Tag;
            if (idx >= 0 && idx < _currentProngRotations.Count)
            { _currentProngRotations[idx] = st.Value; UpdatePreview(); }
        }

        private void OnSortSliderChanged(object sender, EventArgs e)
        {
            if (_suspendUpdates) return;
            var sl = sender as Slider; if (sl == null) return;
            int idx = (int)sl.Tag;
            double nv = sl.Value / 100.0, ov = _currentProngPositions[idx];
            if (_chkMoveAll.Checked == true)
            {
                double d = nv - ov; _suspendUpdates = true;
                for (int i = 0; i < _prongSliders.Count; i++)
                {
                    double sh = _currentProngPositions[i] + d;
                    int sv = (int)(sh * 100); while (sv > 100) sv -= 100; while (sv < 0) sv += 100;
                    _prongSliders[i].Value = sv; _currentProngPositions[i] = sh;
                }
                _suspendUpdates = false;
            }
            else _currentProngPositions[idx] = nv;
            UpdatePreview();
        }

        // ===============================================================
        // GEM SELECTION
        // ===============================================================

        private void OnSelectGem(object sender, EventArgs e)
        {
            var go = new GetObject(); go.SetCommandPrompt("Select Gems");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh; go.EnablePreSelect(true, true);
            Visible = false; go.GetMultiple(1, 0); Visible = true;
            if (go.CommandResult() == Rhino.Commands.Result.Success)
            {
                _selectedGems.Clear(); double ts = 0; int cnt = 0;
                foreach (var objRef in go.Objects())
                {
                    var obj = objRef.Object();
                    if (RhinoGoldHelper.TryGetGemData(obj, out Curve c, out Plane p, out double s))
                    { _selectedGems.Add(new NewRhinoGold.Helpers.GemData { Id = obj.Id, Curve = c, Plane = p, Size = s }); ts += s; cnt++; }
                }
                if (_selectedGems.Count > 0)
                {
                    _btnSelectGem.Text = $"{_selectedGems.Count} Gems Selected";
                    if (!_suspendUpdates && cnt > 0)
                    {
                        bool o = _suspendUpdates; _suspendUpdates = true;
                        double ps = (ts / cnt) * 0.30;
                        _numTopDia.Value = ps; _numMidDia.Value = ps; _numBotDia.Value = ps;
                        _suspendUpdates = o;
                    }
                    UpdatePreview();
                }
            }
        }

        // ===============================================================
        // PREVIEW
        // ===============================================================

        private void UpdatePreview()
        {
            if (_suspendUpdates || _selectedGems.Count == 0) return;
            var param = GetParameters();
            var allBreps = new List<Brep>();
            var debugCurves = new List<Curve>();
            foreach (var gem in _selectedGems)
            {
                var parts = HeadBuilder.CreateHead(gem.Curve, gem.Plane, param);
                if (parts != null) allBreps.AddRange(parts);
                if (gem.Curve != null) debugCurves.Add(gem.Curve.DuplicateCurve());
            }
            CalculateWeight(allBreps);
            _previewConduit.SetColor(_headColor);
            _previewConduit.SetBreps(allBreps.ToArray());
            _previewConduit.SetCurves(debugCurves.ToArray());
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        // ===============================================================
        // BUILD
        // ===============================================================

        private void OnBuild(object sender, EventArgs e)
        {
            if (_selectedGems.Count == 0) return;
            var param = GetParameters();
            var doc = RhinoDoc.ActiveDoc;
            string metalName = GetSelectedMetalName() ?? "Metal";

            uint sn = doc.BeginUndoRecord("Create/Update Head");
            try
            {
                foreach (var gem in _selectedGems)
                {
                    var parts = HeadBuilder.CreateHead(gem.Curve, gem.Plane, param);
                    if (parts == null || parts.Count == 0) continue;

                    foreach (var part in parts)
                    {
                        if (!part.IsValid) part.Repair(doc.ModelAbsoluteTolerance);
                        if (!part.IsSolid) part.CapPlanarHoles(doc.ModelAbsoluteTolerance);
                    }

                    int gi = -1;
                    if (_editingObjectId != Guid.Empty)
                    {
                        var old = doc.Objects.FindId(_editingObjectId);
                        if (old != null)
                        {
                            var gl = old.Attributes.GetGroupList();
                            if (gl != null && gl.Length > 0)
                            { gi = gl[0]; foreach (var g in doc.Objects.FindByGroup(gi)) doc.Objects.Delete(g, true); }
                            else { doc.Objects.Delete(_editingObjectId, true); gi = doc.Groups.Add("SmartHead_Group"); }
                        }
                    }
                    if (gi == -1) gi = doc.Groups.Add("SmartHead_Group");

                    foreach (var b in parts)
                    {
                        var attr = doc.CreateDefaultAttributes();
                        attr.Name = "SmartHead";
                        attr.SetUserString("RG_TYPE", "Head");
                        attr.SetUserString("RG MATERIAL ID", metalName);
                        attr.AddToGroup(gi);
                        attr.ObjectColor = _headColor;
                        attr.ColorSource = ObjectColorSource.ColorFromObject;
                        b.UserData.Add(new HeadSmartData(param, gem.Id, gem.Plane));
                        doc.Objects.AddBrep(b, attr);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            finally { doc.EndUndoRecord(sn); }
            doc.Views.Redraw(); Close();
        }

        // ===============================================================
        // ADD PROFILE
        // ===============================================================

        private void OnAddProfileFromRhino(object sender, EventArgs e)
        {
            var go = new GetObject(); go.SetCommandPrompt("Select Planar Curve");
            go.GeometryFilter = ObjectType.Curve;
            Visible = false; go.Get(); Visible = true;
            if (go.CommandResult() == Rhino.Commands.Result.Success)
            {
                var crv = go.Object(0).Curve();
                if (crv != null && crv.IsPlanar())
                {
                    var dlg = new TextInputDialog("Profile Name", "Custom");
                    string name = dlg.ShowModal(this);
                    if (!string.IsNullOrEmpty(name) && ProfileLoader.SaveProfile(name, crv, "Curves"))
                    {
                        var items = HeadProfileLibrary.GetProfileItems();
                        _gridProfiles.DataStore = items;
                        _gridTopRailProfile.DataStore = HeadProfileLibrary.GetProfileItems();
                        _gridBotRailProfile.DataStore = HeadProfileLibrary.GetProfileItems();
                    }
                }
            }
        }

        // ===============================================================
        // PARAMETERS
        // ===============================================================

        private HeadParameters GetParameters()
        {
            if (_numProngCount == null) return new HeadParameters();
            var p = new HeadParameters();
            p.ProngCount = (int)_numProngCount.Value; p.GemInside = _numGemInside.Value;
            p.TopDiameter = _numTopDia.Value; p.MidDiameter = _numMidDia.Value; p.BottomDiameter = _numBotDia.Value;
            p.TopOffset = _numTopOff.Value; p.MidOffset = _numMidOff.Value; p.BottomOffset = _numBotOff.Value;
            p.DepthBelowGem = _numDepthBelow.Value; p.Height = _numHeight.Value;
            p.TopProfileRotation = _numTopRot.Value; p.MidProfileRotation = _numMidRot.Value; p.BottomProfileRotation = _numBotRot.Value;

            p.EnableTopRail = _chkTopRail.Checked ?? false;
            p.TopRailWidth = _numTopRailWidth.Value; p.TopRailThickness = _numTopRailHeight.Value;
            p.TopRailPosition = _numTopRailPos.Value; p.TopRailOffset = _numTopRailOffset.Value;
            p.TopRailRotation = _numTopRailRot.Value;
            p.TopRailProfileName = GetSelectedProfileName(_gridTopRailProfile) ?? "Round";

            p.EnableBottomRail = _chkBotRail.Checked ?? false;
            p.BottomRailWidth = _numBotRailWidth.Value; p.BottomRailThickness = _numBotRailHeight.Value;
            p.BottomRailPosition = _numBotRailPos.Value; p.BottomRailOffset = _numBotRailOffset.Value;
            p.BottomRailRotation = _numBotRailRot.Value;
            p.BottomRailProfileName = GetSelectedProfileName(_gridBotRailProfile) ?? "Round";

            if (_gridProfiles.SelectedItem is HeadProfileItem pi) p.ProfileName = pi.Name;

            p.ProngPositions = _currentProngPositions.Count > 0
                ? new List<double>(_currentProngPositions)
                : Enumerable.Range(0, p.ProngCount).Select(i => (double)i / p.ProngCount).ToList();

            p.ProngRotations = _currentProngRotations.Count > 0
                ? new List<double>(_currentProngRotations)
                : Enumerable.Repeat(0.0, p.ProngCount).ToList();

            return p;
        }

        // ===============================================================
        // LOAD SMART DATA
        // ===============================================================

        public void LoadSmartData(HeadSmartData data, Guid objectId)
        {
            if (data == null) return;
            _editingObjectId = objectId; _btnBuild.Text = "Update"; _suspendUpdates = true;

            _numProngCount.Value = data.ProngCount; RebuildSortList(data.ProngCount);
            _numGemInside.Value = data.GemInside;
            _numTopDia.Value = data.TopDiameter; _numMidDia.Value = data.MidDiameter; _numBotDia.Value = data.BottomDiameter;
            _numTopOff.Value = data.TopOffset; _numMidOff.Value = data.MidOffset; _numBotOff.Value = data.BottomOffset;
            _numDepthBelow.Value = data.DepthBelowGem; _numHeight.Value = data.Height;
            _numTopRot.Value = data.TopProfileRotation; _numMidRot.Value = data.MidProfileRotation; _numBotRot.Value = data.BottomProfileRotation;

            SelectProfileByName(_gridProfiles, data.ProfileName);
            SelectProfileByName(_gridTopRailProfile, data.TopRailProfileName);
            SelectProfileByName(_gridBotRailProfile, data.BottomRailProfileName);

            _chkTopRail.Checked = data.EnableTopRail;
            _numTopRailWidth.Value = data.TopRailWidth; _numTopRailHeight.Value = data.TopRailThickness;
            _numTopRailPos.Value = data.TopRailPosition; _numTopRailOffset.Value = data.TopRailOffset;
            _numTopRailRot.Value = data.TopRailRotation;

            _chkBotRail.Checked = data.EnableBottomRail;
            _numBotRailWidth.Value = data.BottomRailWidth; _numBotRailHeight.Value = data.BottomRailThickness;
            _numBotRailPos.Value = data.BottomRailPosition; _numBotRailOffset.Value = data.BottomRailOffset;
            _numBotRailRot.Value = data.BottomRailRotation;

            if (data.ProngPositions != null && data.ProngPositions.Count == _prongSliders.Count)
            {
                _currentProngPositions = new List<double>(data.ProngPositions);
                for (int i = 0; i < _prongSliders.Count; i++)
                    _prongSliders[i].Value = (int)(_currentProngPositions[i] * 100);
            }
            if (data.ProngRotations != null && data.ProngRotations.Count == _prongRotationSteppers.Count)
            {
                _currentProngRotations = new List<double>(data.ProngRotations);
                for (int i = 0; i < _prongRotationSteppers.Count; i++)
                    _prongRotationSteppers[i].Value = _currentProngRotations[i];
            }

            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                var existingObj = doc.Objects.FindId(objectId);
                if (existingObj != null)
                {
                    if (existingObj.Attributes.ColorSource == ObjectColorSource.ColorFromObject)
                        _headColor = existingObj.Attributes.ObjectColor;
                    string savedMat = existingObj.Attributes.GetUserString("RG MATERIAL ID");
                    if (!string.IsNullOrEmpty(savedMat))
                        for (int i = 0; i < _drpMetal.Items.Count; i++)
                        {
                            var li = _drpMetal.Items[i] as ListItem;
                            if (li != null && (li.Text == savedMat || li.Key == savedMat))
                            { _drpMetal.SelectedIndex = i; break; }
                        }
                }

                var gemObj = doc.Objects.FindId(data.GemId);
                if (gemObj != null && RhinoGoldHelper.TryGetGemData(gemObj, out Curve c, out Plane p, out double s))
                {
                    _selectedGems.Clear();
                    _selectedGems.Add(new NewRhinoGold.Helpers.GemData { Id = gemObj.Id, Curve = c, Plane = p, Size = s });
                    _btnSelectGem.Text = "Gem Loaded";
                }
            }
            _suspendUpdates = false; UpdatePreview();
        }
    }
}