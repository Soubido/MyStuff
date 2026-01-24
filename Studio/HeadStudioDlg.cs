using System;
using System.Collections.Generic;
using System.Linq; // Wichtig für LINQ Operationen
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.UI;
using NewRhinoGold.Core;
using NewRhinoGold.Helpers;
using NewRhinoGold.BezelStudio; // WICHTIG: Hier liegt der HeadBuilder

namespace NewRhinoGold.Studio
{
    public class HeadStudioDlg : Form
    {
        private readonly GemDisplayCond _previewConduit;
        private Guid _editingObjectId = Guid.Empty;
        private List<NewRhinoGold.Helpers.GemData> _selectedGems = new List<NewRhinoGold.Helpers.GemData>();

        // Styles
        private readonly Eto.Drawing.Font _fontStandard = Eto.Drawing.Fonts.Sans(10);
        private readonly Eto.Drawing.Font _fontInput = Eto.Drawing.Fonts.Sans(11);
        private readonly Eto.Drawing.Font _fontHeader = Eto.Drawing.Fonts.Sans(12, FontStyle.Bold);
        private readonly Eto.Drawing.Font _fontLabelBold = Eto.Drawing.Fonts.Sans(10, FontStyle.Bold);

        // UI Controls
        private Button _btnSelectGem;
        private TabControl _tabControl;
        private Button _btnBuild;
        private Button _btnClose;

        // General
        private NumericStepper _numGemInside, _numProngCount;
        private CheckBox _chkLockDia;
        private NumericStepper _numTopDia, _numMidDia, _numBotDia;
        private NumericStepper _numTopOff, _numMidOff, _numBotOff;
        private NumericStepper _numDepthBelow, _numHeight;

        // Profile
        private GridView _gridProfiles;
        private Button _btnAddProfile;
        private NumericStepper _numTopRot, _numMidRot, _numBotRot;

        // Rails
        private CheckBox _chkTopRail;
        private DropDown _drpTopProfile;
        private NumericStepper _numTopRailWidth, _numTopRailHeight, _numTopRailPos, _numTopRailOffset, _numTopRailRot;

        private CheckBox _chkBotRail;
        private DropDown _drpBotProfile;
        private NumericStepper _numBotRailWidth, _numBotRailHeight, _numBotRailPos, _numBotRailOffset, _numBotRailRot;

        // Sort
        private CheckBox _chkMoveAll;
        private Scrollable _scrollSort;
        private StackLayout _sortLayout;
        private List<Slider> _prongSliders = new List<Slider>();
        private List<double> _currentProngPositions = new List<double>();

        private bool _suspendUpdates = true;

        public HeadStudioDlg()
        {
            _suspendUpdates = true;
            Title = "Head Studio";
            ClientSize = new Size(360, 500);
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

            var footer = new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows = { new TableRow(new TableCell(_btnBuild, true), new TableCell(_btnClose, true)) }
            };
            layout.AddRow(footer);
            layout.EndVertical();
            return layout;
        }

        private Control BuildGeneralTab()
        {
            _numProngCount = CreateStepper(4, 0, 1.0);
            _numProngCount.ValueChanged += (s, e) => {
                if (!_suspendUpdates) RebuildSortList((int)_numProngCount.Value);
                UpdatePreview();
            };
            _numGemInside = CreateStepper(30, 0, 1.0);
            _numTopDia = CreateStepper(1.0); _numMidDia = CreateStepper(1.0); _numBotDia = CreateStepper(1.0);
            _chkLockDia = new CheckBox { Text = "Lock", Checked = true, ToolTip = "Synchronize", Font = _fontStandard };

            void SyncDia(double val)
            {
                if (_chkLockDia.Checked == true && !_suspendUpdates)
                {
                    bool old = _suspendUpdates; _suspendUpdates = true;
                    _numTopDia.Value = val; _numMidDia.Value = val; _numBotDia.Value = val;
                    _suspendUpdates = old;
                    UpdatePreview();
                }
                else UpdatePreview();
            }
            _numTopDia.ValueChanged += (s, e) => SyncDia(_numTopDia.Value);
            _numMidDia.ValueChanged += (s, e) => SyncDia(_numMidDia.Value);
            _numBotDia.ValueChanged += (s, e) => SyncDia(_numBotDia.Value);

            _numTopOff = CreateStepper(0.0); _numMidOff = CreateStepper(0.0); _numBotOff = CreateStepper(0.0);
            _numDepthBelow = CreateStepper(2.0); _numHeight = CreateStepper(5.0);

            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 8) };
            layout.BeginVertical();
            layout.AddRow(CreateLabel("Prong Count:"), _numProngCount);
            layout.AddRow(CreateLabel("Gem Inside (%):"), _numGemInside);
            layout.AddRow(null);

            var gridHeader = new TableLayout
            {
                Spacing = new Size(5, 0),
                Rows = { new TableRow(new Label { Text = "Level", Font = _fontLabelBold, Width = 40 }, new Label { Text = "Diameter", Font = _fontLabelBold }, _chkLockDia, new Label { Text = "Shift", Font = _fontLabelBold }) }
            };
            layout.AddRow(gridHeader);
            TableRow CreateGridRow(string label, Control dia, Control off) => new TableRow(new TableCell(new Label { Text = label, VerticalAlignment = VerticalAlignment.Center, Font = _fontStandard }, false), new TableCell(dia, true), new TableCell(new Panel { Width = 10 }, false), new TableCell(off, true));
            var grid = new TableLayout { Spacing = new Size(5, 5) };
            grid.Rows.Add(CreateGridRow("Top:", _numTopDia, _numTopOff));
            grid.Rows.Add(CreateGridRow("Mid:", _numMidDia, _numMidOff));
            grid.Rows.Add(CreateGridRow("Bot:", _numBotDia, _numBotOff));
            layout.AddRow(grid);
            layout.AddRow(null);
            layout.AddRow(CreateHeader("Dimensions"));
            layout.AddRow(CreateLabel("Depth below Gem:"), _numDepthBelow);
            layout.AddRow(CreateLabel("Total Height:"), _numHeight);
            layout.EndVertical();
            return new Scrollable { Content = layout, Border = BorderType.None };
        }

        private Control BuildProngProfileTab()
        {
            _gridProfiles = new GridView { Height = 200, AllowMultipleSelection = false };
            _gridProfiles.Columns.Add(new GridColumn
            {
                HeaderText = "Profile",
                Width = 200,
                DataCell = new ImageTextCell
                {
                    ImageBinding = Binding.Property<HeadProfileItem, Image>(x => x.Preview),
                    TextBinding = Binding.Property<HeadProfileItem, string>(x => x.Name)
                }
            });

            // Laden aus Library
            _gridProfiles.DataStore = HeadProfileLibrary.GetProfileItems();
            _gridProfiles.SelectionChanged += (s, e) => UpdatePreview();

            // Korrigiert: Cast statt AsList()
            var list = (List<HeadProfileItem>)_gridProfiles.DataStore;
            if (list != null && list.Count > 0) _gridProfiles.SelectRow(0);

            _btnAddProfile = new Button { Text = "Add Custom Curve", Font = _fontStandard };
            _btnAddProfile.Click += OnAddProfileFromRhino;

            _numTopRot = CreateStepper(0, 0, 5); _numTopRot.MinValue = 0; _numTopRot.MaxValue = 360;
            _numMidRot = CreateStepper(0, 0, 5); _numMidRot.MinValue = 0; _numMidRot.MaxValue = 360;
            _numBotRot = CreateStepper(0, 0, 5); _numBotRot.MinValue = 0; _numBotRot.MaxValue = 360;

            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };
            layout.BeginVertical();
            layout.AddRow(CreateHeader("Select Profile"));
            layout.AddRow(_gridProfiles);
            layout.AddRow(_btnAddProfile);
            layout.AddRow(null);
            layout.AddRow(CreateHeader("Profile Rotation (°)"));
            layout.AddRow(CreateLabel("Top:"), _numTopRot);
            layout.AddRow(CreateLabel("Mid:"), _numMidRot);
            layout.AddRow(CreateLabel("Bot:"), _numBotRot);
            layout.EndVertical();
            return new Scrollable { Content = layout, Border = BorderType.None };
        }

        private Control BuildAuflageTab()
        {
            void FillDrp(DropDown d)
            {
                d.Items.Clear();
                foreach (var name in HeadProfileLibrary.GetProfileNames())
                    d.Items.Add(new ListItem { Text = name, Key = name });
                if (d.Items.Count > 0) d.SelectedIndex = 0;
            }

            _chkTopRail = new CheckBox { Text = "Enable Top Rail", Checked = true, Font = _fontStandard }; _chkTopRail.CheckedChanged += (s, e) => UpdatePreview();
            _drpTopProfile = new DropDown { Font = _fontStandard }; FillDrp(_drpTopProfile); _drpTopProfile.SelectedValueChanged += (s, e) => UpdatePreview();
            _numTopRailWidth = CreateStepper(0.8); _numTopRailHeight = CreateStepper(0.8); _numTopRailPos = CreateStepper(-0.5); _numTopRailOffset = CreateStepper(0.0); _numTopRailRot = CreateStepper(0, 0, 5);

            Control BuildRailGroup(string title, CheckBox enable, DropDown prof, NumericStepper w, NumericStepper h, NumericStepper z, NumericStepper off, NumericStepper rot)
            {
                var gl = new DynamicLayout { Padding = 5, Spacing = new Size(5, 5) };
                gl.BeginVertical(); gl.AddRow(enable); gl.AddRow(CreateLabel("Profile:"), prof);
                var sizeRow = new TableLayout { Spacing = new Size(5, 0), Rows = { new TableRow(w, new Label { Text = "x", VerticalAlignment = VerticalAlignment.Center }, h) } };
                gl.AddRow(CreateLabel("Size (WxH):"), sizeRow); gl.AddRow(CreateLabel("Z-Pos:"), z); gl.AddRow(CreateLabel("Offset:"), off); gl.AddRow(CreateLabel("Rotation:"), rot);
                gl.EndVertical(); return new GroupBox { Text = title, Content = gl, Font = _fontStandard };
            }
            var topGroup = BuildRailGroup("Top Rail", _chkTopRail, _drpTopProfile, _numTopRailWidth, _numTopRailHeight, _numTopRailPos, _numTopRailOffset, _numTopRailRot);

            _chkBotRail = new CheckBox { Text = "Enable Bottom Rail", Checked = true, Font = _fontStandard }; _chkBotRail.CheckedChanged += (s, e) => UpdatePreview();
            _drpBotProfile = new DropDown { Font = _fontStandard }; FillDrp(_drpBotProfile); _drpBotProfile.SelectedValueChanged += (s, e) => UpdatePreview();
            _numBotRailWidth = CreateStepper(0.8); _numBotRailHeight = CreateStepper(0.8); _numBotRailPos = CreateStepper(-2.0); _numBotRailOffset = CreateStepper(0.0); _numBotRailRot = CreateStepper(0, 0, 5);
            var botGroup = BuildRailGroup("Bottom Rail", _chkBotRail, _drpBotProfile, _numBotRailWidth, _numBotRailHeight, _numBotRailPos, _numBotRailOffset, _numBotRailRot);

            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };
            layout.BeginVertical(); layout.AddRow(topGroup); layout.AddRow(null); layout.AddRow(botGroup); layout.EndVertical();
            return new Scrollable { Content = layout, Border = BorderType.None };
        }

        private Control BuildSortTab()
        {
            _chkMoveAll = new CheckBox { Text = "Move All together", Font = _fontStandard };
            _sortLayout = new StackLayout { Spacing = 5, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            _scrollSort = new Scrollable { Content = _sortLayout, Border = BorderType.Bezel, Height = 300 };
            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };
            layout.BeginVertical(); layout.AddRow(_chkMoveAll); layout.AddRow(new Label { Text = "Prong Positions on Curve (0-100%):", Font = _fontStandard }); layout.AddRow(_scrollSort); layout.EndVertical();
            return layout;
        }

        private Label CreateHeader(string text) => new Label { Text = text, Font = _fontHeader, TextColor = Colors.Gray };
        private Label CreateLabel(string text) => new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, Font = _fontStandard };
        private NumericStepper CreateStepper(double v, int d = 2, double inc = 0.1) { var s = new NumericStepper { Value = v, DecimalPlaces = d, Increment = inc, Font = _fontInput, Height = 26 }; s.ValueChanged += (sender, e) => UpdatePreview(); return s; }

        private void RebuildSortList(int count)
        {
            if (_sortLayout == null) return;
            bool old = _suspendUpdates; _suspendUpdates = true;
            _sortLayout.Items.Clear(); _prongSliders.Clear(); _currentProngPositions.Clear();
            for (int i = 0; i < count; i++)
            {
                double pos = (double)i / count; _currentProngPositions.Add(pos);
                var slider = new Slider { MinValue = 0, MaxValue = 100, Value = (int)(pos * 100), Tag = i };
                slider.ValueChanged += OnSortSliderChanged; _prongSliders.Add(slider);
                _sortLayout.Items.Add(new TableLayout { Rows = { new TableRow(new Label { Text = $"P{i + 1}", Width = 40, Font = _fontStandard }, slider) } });
            }
            _suspendUpdates = old;
        }

        private void OnSortSliderChanged(object sender, EventArgs e)
        {
            if (_suspendUpdates) return;
            var slider = sender as Slider; if (slider == null) return;
            int idx = (int)slider.Tag; int val = slider.Value; double newValP = val / 100.0; double oldValP = _currentProngPositions[idx];
            if (_chkMoveAll.Checked == true)
            {
                double diff = newValP - oldValP; _suspendUpdates = true;
                for (int i = 0; i < _prongSliders.Count; i++)
                {
                    double current = _currentProngPositions[i]; double shifted = current + diff;
                    int sVal = (int)(shifted * 100); while (sVal > 100) sVal -= 100; while (sVal < 0) sVal += 100;
                    _prongSliders[i].Value = sVal; _currentProngPositions[i] = shifted;
                }
                _suspendUpdates = false;
            }
            else _currentProngPositions[idx] = newValP;
            UpdatePreview();
        }

        private void OnSelectGem(object sender, EventArgs e)
        {
            var go = new GetObject(); go.SetCommandPrompt("Select Gems"); go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh; go.EnablePreSelect(true, true);
            Visible = false; go.GetMultiple(1, 0); Visible = true;
            if (go.CommandResult() == Rhino.Commands.Result.Success)
            {
                _selectedGems.Clear(); double totalSize = 0; int count = 0;
                foreach (var objRef in go.Objects())
                {
                    var obj = objRef.Object();
                    if (RhinoGoldHelper.TryGetGemData(obj, out Curve c, out Plane p, out double s)) { _selectedGems.Add(new NewRhinoGold.Helpers.GemData { Id = obj.Id, Curve = c, Plane = p, Size = s }); totalSize += s; count++; }
                }
                if (_selectedGems.Count > 0)
                {
                    _btnSelectGem.Text = $"{_selectedGems.Count} Gems Selected";
                    if (!_suspendUpdates && count > 0) { bool oldSuspend = _suspendUpdates; _suspendUpdates = true; double avgSize = totalSize / count; double prongSize = avgSize * 0.30; _numTopDia.Value = prongSize; _numMidDia.Value = prongSize; _numBotDia.Value = prongSize; _suspendUpdates = oldSuspend; }
                    UpdatePreview();
                }
            }
        }

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
            _previewConduit.setbreps(allBreps.ToArray());
            _previewConduit.setcurves(debugCurves.ToArray());
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        private void OnBuild(object sender, EventArgs e)
        {
            if (_selectedGems.Count == 0) return;
            var param = GetParameters();
            var doc = RhinoDoc.ActiveDoc;

            // Undo Record starten
            uint sn = doc.BeginUndoRecord("Create/Update Head");

            try
            {
                foreach (var gem in _selectedGems)
                {
                    // 1. Geometrie erzeugen
                    var parts = HeadBuilder.CreateHead(gem.Curve, gem.Plane, param);

                    if (parts != null && parts.Count > 0)
                    {
                        // Reparatur und Cap
                        foreach (var part in parts)
                        {
                            if (!part.IsValid) part.Repair(doc.ModelAbsoluteTolerance);
                            // Nur schließen, wenn es keine Rail ist (Rails sind manchmal offen gewollt, hier Annahme solid)
                            if (!part.IsSolid) part.CapPlanarHoles(doc.ModelAbsoluteTolerance);
                        }

                        // 2. SmartData vorbereiten
                        // Wir erzeugen für jedes Brep eine neue Instanz oder (besser) nutzen OnDuplicate beim Add
                        // Aber hier erstellen wir einfach eines und lassen Rhino kopieren beim Add
                        var smartDataTemplate = new HeadSmartData(param, gem.Id, gem.Plane);

                        // 3. Lösch-Logik für UPDATE
                        int groupIndex = -1;

                        if (_editingObjectId != Guid.Empty)
                        {
                            // Wir sind im Edit-Modus. Wir müssen das ALTE Zeug löschen.
                            var oldObj = doc.Objects.FindId(_editingObjectId);
                            if (oldObj != null)
                            {
                                // Prüfen, ob das Objekt Teil einer Gruppe ist
                                var attrs = oldObj.Attributes;
                                var groupList = attrs.GetGroupList();

                                if (groupList != null && groupList.Length > 0)
                                {
                                    // Wir nehmen die erste Gruppe an
                                    groupIndex = groupList[0];
                                    var groupObjects = doc.Objects.FindByGroup(groupIndex);

                                    // Alle Objekte der alten Gruppe löschen
                                    foreach (var go in groupObjects) doc.Objects.Delete(go, true);
                                }
                                else
                                {
                                    // Kein Gruppenobjekt, also nur Einzelobjekt löschen
                                    doc.Objects.Delete(_editingObjectId, true);

                                    // Neue Gruppe anlegen für das neue Objekt
                                    groupIndex = doc.Groups.Add("SmartHead_Group");
                                }
                            }
                        }

                        // Falls wir keine Gruppe haben (Neu erstellen oder Fallback), neue anlegen
                        if (groupIndex == -1)
                        {
                            groupIndex = doc.Groups.Add("SmartHead_Group");
                        }

                        // 4. Neue Objekte hinzufügen
                        foreach (var b in parts)
                        {
                            var attr = doc.CreateDefaultAttributes();
                            attr.Name = "SmartHead";
                            attr.SetUserString("RG_TYPE", "Head");

                            // Der Gruppe zuweisen
                            attr.AddToGroup(groupIndex);

                            // UserData hinzufügen (Wichtig: Rhino macht keine automatische DeepCopy beim AddUserData, 
                            // wenn wir das selbe Objekt verwenden. Daher sicherheitshalber neu instanziieren oder duplizieren,
                            // falls das Objekt referenziert wird. Hier: Einfachheit halber nutzen wir das Template, 
                            // Rhino serialisiert es beim Speichern eh separat für jedes Objekt.)
                            // BESSER: Explizites Duplikat für jedes Teil, um Seiteneffekte im Speicher zu vermeiden.
                            var dataCopy = new HeadSmartData(param, gem.Id, gem.Plane);
                            b.UserData.Add(dataCopy);

                            doc.Objects.AddBrep(b, attr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error building head: {ex.Message}");
            }
            finally
            {
                doc.EndUndoRecord(sn);
            }

            doc.Views.Redraw();
            Close();
        }

        private void OnAddProfileFromRhino(object sender, EventArgs e)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select Planar Curve");
            go.GeometryFilter = ObjectType.Curve;
            Visible = false;
            go.Get();
            Visible = true;

            if (go.CommandResult() == Rhino.Commands.Result.Success)
            {
                var crv = go.Object(0).Curve();
                if (crv != null && crv.IsPlanar())
                {
                    var dlg = new TextInputDialog("Profile Name", "Custom");
                    string name = dlg.ShowModal(this);
                    if (!string.IsNullOrEmpty(name))
                    {
                        // Speichern in "Curves" Ordner
                        bool ok = ProfileLoader.SaveProfile(name, crv, "Curves");
                        if (ok)
                        {
                            _gridProfiles.DataStore = HeadProfileLibrary.GetProfileItems();
                            void RefreshDrp(DropDown d)
                            {
                                d.Items.Clear();
                                foreach (var n in HeadProfileLibrary.GetProfileNames())
                                    d.Items.Add(new ListItem { Text = n, Key = n });
                            }
                            RefreshDrp(_drpTopProfile);
                            RefreshDrp(_drpBotProfile);
                        }
                    }
                }
            }
        }

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
            p.TopRailWidth = _numTopRailWidth.Value; p.TopRailThickness = _numTopRailHeight.Value; p.TopRailPosition = _numTopRailPos.Value; p.TopRailOffset = _numTopRailOffset.Value; p.TopRailRotation = _numTopRailRot.Value;
            if (_drpTopProfile.SelectedKey != null) p.TopRailProfileName = _drpTopProfile.SelectedKey;

            p.EnableBottomRail = _chkBotRail.Checked ?? false;
            p.BottomRailWidth = _numBotRailWidth.Value; p.BottomRailThickness = _numBotRailHeight.Value; p.BottomRailPosition = _numBotRailPos.Value; p.BottomRailOffset = _numBotRailOffset.Value; p.BottomRailRotation = _numBotRailRot.Value;
            if (_drpBotProfile.SelectedKey != null) p.BottomRailProfileName = _drpBotProfile.SelectedKey;

            if (_gridProfiles.SelectedItem is HeadProfileItem pi) p.ProfileName = pi.Name;

            if (_currentProngPositions.Count > 0) p.ProngPositions = new List<double>(_currentProngPositions);
            else { p.ProngPositions = new List<double>(); for (int i = 0; i < p.ProngCount; i++) p.ProngPositions.Add((double)i / p.ProngCount); }
            return p;
        }

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

            // Profil Selektierung über Namen
            if (!string.IsNullOrEmpty(data.ProfileName))
            {
                var list = (List<HeadProfileItem>)_gridProfiles.DataStore;
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                        if (list[i].Name == data.ProfileName) { _gridProfiles.SelectRow(i); break; }
                }
            }

            _chkTopRail.Checked = data.EnableTopRail; _numTopRailWidth.Value = data.TopRailWidth; _numTopRailHeight.Value = data.TopRailThickness; _numTopRailPos.Value = data.TopRailPosition; _numTopRailOffset.Value = data.TopRailOffset; _numTopRailRot.Value = data.TopRailRotation;
            if (!string.IsNullOrEmpty(data.TopRailProfileName)) _drpTopProfile.SelectedKey = data.TopRailProfileName;

            _chkBotRail.Checked = data.EnableBottomRail; _numBotRailWidth.Value = data.BottomRailWidth; _numBotRailHeight.Value = data.BottomRailThickness; _numBotRailPos.Value = data.BottomRailPosition; _numBotRailOffset.Value = data.BottomRailOffset; _numBotRailRot.Value = data.BottomRailRotation;
            if (!string.IsNullOrEmpty(data.BottomRailProfileName)) _drpBotProfile.SelectedKey = data.BottomRailProfileName;

            if (data.ProngPositions != null && data.ProngPositions.Count == _prongSliders.Count) { _currentProngPositions = new List<double>(data.ProngPositions); for (int i = 0; i < _prongSliders.Count; i++) _prongSliders[i].Value = (int)(_currentProngPositions[i] * 100); }
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null) { var gemObj = doc.Objects.FindId(data.GemId); if (gemObj != null && RhinoGoldHelper.TryGetGemData(gemObj, out Curve c, out Plane p, out double s)) { _selectedGems.Clear(); _selectedGems.Add(new NewRhinoGold.Helpers.GemData { Id = gemObj.Id, Curve = c, Plane = p, Size = s }); _btnSelectGem.Text = "Gem Loaded"; } }
            _suspendUpdates = false; UpdatePreview();
        }
    }
}