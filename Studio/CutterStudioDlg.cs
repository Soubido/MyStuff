using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using NewRhinoGold.Core;
using NewRhinoGold.BezelStudio;
using NewRhinoGold.Helpers;

namespace NewRhinoGold.Studio
{
    public class CutterStudioDlg : Dialog<bool>
    {
        // --- STATUS ---
        private List<GemSmartData> _selectedGems = new List<GemSmartData>();
        private CutterPreviewConduit _previewConduit;
        private bool _suspendUpdates = false;

        // --- UI CONTROLS ---
        private Button _btnSelectGems, _btnBuild, _btnClose;

        // Parameter
        private NumericStepper _numScale, _numClearance;
        private NumericStepper _numTopHeight, _numTopDia;
        private NumericStepper _numSeatPos;
        private NumericStepper _numBotHeight, _numBotDia;
        private NumericStepper _numProfileRot;

        // Shape Mode
        private RadioButton _rbShapeRound, _rbShapeLibrary;
        private GridView _gridLibrary;

        private TabControl _tabControl;

        public CutterStudioDlg()
        {
            Title = "Cutter Studio";
            // Kompakte Größe wie angefordert
            ClientSize = new Size(380, 450);
            Topmost = true;
            Resizable = false;

            _previewConduit = new CutterPreviewConduit();

            Content = BuildLayout();

            // Events für Vorschau-Handling
            Shown += (s, e) => { _previewConduit.Enabled = true; UpdatePreview(); };
            Closed += (s, e) => { _previewConduit.Enabled = false; RhinoDoc.ActiveDoc?.Views.Redraw(); };
        }

        private Control BuildLayout()
        {
            // 1. HEADER AREA
            _btnSelectGems = new Button { Text = "Select Gems", Height = 28 };
            _btnSelectGems.Click += OnSelectGems;

            var topLayout = new TableLayout { Padding = new Padding(10, 10, 10, 5), Spacing = new Size(5, 5) };
            topLayout.Rows.Add(new TableRow(_btnSelectGems));

            // 2. TABS AREA
            _tabControl = new TabControl();
            _tabControl.Pages.Add(new TabPage { Text = "Parameters", Content = BuildParamsTab() });
            _tabControl.Pages.Add(new TabPage { Text = "Bottom Shape", Content = BuildShapeTab() });

            // 3. ACTION AREA
            _btnBuild = new Button { Text = "Build Cutters", Height = 28, Font = Eto.Drawing.Fonts.Sans(9, FontStyle.Bold) };
            _btnBuild.Click += OnBuild;

            _btnClose = new Button { Text = "Close", Height = 28 };
            _btnClose.Click += (s, e) => Close(false);

            var actionsLayout = new TableLayout { Spacing = new Size(5, 0) };
            // Rechtsbündige Buttons durch leere Zelle links
            actionsLayout.Rows.Add(new TableRow(null, _btnClose, _btnBuild));

            // MASTER LAYOUT
            var mainLayout = new TableLayout { Padding = 0, Spacing = new Size(0, 0) };
            mainLayout.Rows.Add(topLayout);
            mainLayout.Rows.Add(new TableRow(_tabControl) { ScaleHeight = true });

            // Trennlinie
            mainLayout.Rows.Add(new TableRow(new Panel { BackgroundColor = Colors.LightGrey, Height = 1 }));
            mainLayout.Rows.Add(new TableRow(new Panel { Content = actionsLayout, Padding = 10 }));

            return mainLayout;
        }

        private Control BuildParamsTab()
        {
            // Stepper Initialisierung
            _numScale = CreateStepper(100, 0);
            _numClearance = CreateStepper(0.10, 2);
            _numTopHeight = CreateStepper(100, 0);
            _numTopDia = CreateStepper(100, 0);
            _numSeatPos = CreateStepper(30, 0);
            _numBotHeight = CreateStepper(150, 0);
            _numBotDia = CreateStepper(70, 0);

            // Lokale Helper für Zeilen-Layouts
            Control CreatePair(string l1, Control c1, string l2, Control c2)
            {
                var t = new TableLayout { Spacing = new Size(5, 2) };
                t.Rows.Add(new TableRow(
                    new Label { Text = l1, VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) },
                    c1,
                    new Label { Text = l2, VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) },
                    c2
                ));
                return t;
            }

            Control CreateSingle(string l1, Control c1)
            {
                var t = new TableLayout { Spacing = new Size(5, 2) };
                t.Rows.Add(new TableRow(
                    new Label { Text = l1, VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) },
                    c1,
                    null // Füller
                ));
                return t;
            }

            // Group 1: Global
            var grpGlobal = new GroupBox { Text = "Global Settings", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var layGlobal = new TableLayout { Spacing = new Size(5, 5) };
            layGlobal.Rows.Add(CreatePair("Scale %:", _numScale, "Gap:", _numClearance));
            grpGlobal.Content = layGlobal;

            // Group 2: Top Shaft
            var grpTop = new GroupBox { Text = "Top Shaft (%)", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var layTop = new TableLayout { Spacing = new Size(5, 5) };
            layTop.Rows.Add(CreatePair("Height:", _numTopHeight, "Scale:", _numTopDia));
            grpTop.Content = layTop;

            // Group 3: Seat
            var grpSeat = new GroupBox { Text = "Seat / Girdle", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var laySeat = new TableLayout { Spacing = new Size(5, 5) };
            laySeat.Rows.Add(CreateSingle("Seat Level %:", _numSeatPos));
            grpSeat.Content = laySeat;

            // Group 4: Bottom Shaft
            var grpBot = new GroupBox { Text = "Bottom Shaft (%)", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var layBot = new TableLayout { Spacing = new Size(5, 5) };
            layBot.Rows.Add(CreatePair("Height:", _numBotHeight, "Scale:", _numBotDia));
            grpBot.Content = layBot;

            // Stack Layout für vertikale Anordnung
            var finalLayout = new StackLayout
            {
                Padding = 10,
                Spacing = 5,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items = {
                    grpGlobal,
                    grpTop,
                    grpSeat,
                    grpBot,
                    new StackLayoutItem(null, true) // Spacer am Ende drückt alles nach oben
                }
            };

            return new Scrollable { Content = finalLayout, Border = BorderType.None };
        }

        private Control BuildShapeTab()
        {
            _rbShapeRound = new RadioButton { Text = "Standard (Round)", Checked = true, Font = Eto.Drawing.Fonts.Sans(8) };
            _rbShapeLibrary = new RadioButton(_rbShapeRound) { Text = "Library Profile", Font = Eto.Drawing.Fonts.Sans(8) };

            _gridLibrary = new GridView { Height = 150, Enabled = false, Border = BorderType.Bezel };
            _gridLibrary.Columns.Add(new GridColumn
            {
                HeaderText = "Profile",
                Resizable = false,
                AutoSize = true,
                DataCell = new ImageTextCell
                {
                    ImageBinding = Binding.Property<ProfileItem, Image>(x => x.Preview),
                    TextBinding = Binding.Property<ProfileItem, string>(x => x.Name)
                }
            });
            // ProfileLibrary muss existieren (in Core/ProfileLibrary.cs)
            _gridLibrary.DataStore = ProfileLibrary.Items;
            _gridLibrary.SelectionChanged += (s, e) => UpdatePreview();

            _numProfileRot = CreateStepper(0);

            // Logik zum Aktivieren/Deaktivieren der Liste
            void UpdateEnabled()
            {
                _gridLibrary.Enabled = (_rbShapeLibrary.Checked == true);
                UpdatePreview();
            }
            _rbShapeRound.CheckedChanged += (s, e) => UpdateEnabled();
            _rbShapeLibrary.CheckedChanged += (s, e) => UpdateEnabled();

            // Layouts
            var grpMode = new GroupBox { Text = "Shape Mode", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var modeLayout = new TableLayout { Spacing = new Size(5, 5) };
            modeLayout.Rows.Add(_rbShapeRound);
            modeLayout.Rows.Add(_rbShapeLibrary);
            grpMode.Content = modeLayout;

            var grpRot = new GroupBox { Text = "Adjustments", Padding = 5, Font = Eto.Drawing.Fonts.Sans(8, FontStyle.Bold) };
            var rotLayout = new TableLayout { Spacing = new Size(5, 5) };
            rotLayout.Rows.Add(new TableRow(new Label { Text = "Rotation:", VerticalAlignment = VerticalAlignment.Center, Font = Eto.Drawing.Fonts.Sans(8) }, _numProfileRot, null));
            grpRot.Content = rotLayout;

            var mainLayout = new TableLayout { Padding = 10, Spacing = new Size(5, 5) };
            mainLayout.Rows.Add(grpMode);
            mainLayout.Rows.Add(new Label { Text = "Library Selection:", Font = Eto.Drawing.Fonts.Sans(8) });
            mainLayout.Rows.Add(new TableRow(_gridLibrary) { ScaleHeight = true });
            mainLayout.Rows.Add(grpRot);

            return new Scrollable { Content = mainLayout, Border = BorderType.None };
        }

        // --- LOGIK & EVENTS ---

        private CutterParameters GetParameters()
        {
            var p = new CutterParameters();
            p.GlobalScale = _numScale.Value;
            p.Clearance = _numClearance.Value;
            p.TopHeight = _numTopHeight.Value;
            p.TopDiameterScale = _numTopDia.Value;
            p.SeatLevel = _numSeatPos.Value;
            p.BottomHeight = _numBotHeight.Value;
            p.BottomDiameterScale = _numBotDia.Value;

            p.UseCustomProfile = (_rbShapeLibrary.Checked == true);
            if (p.UseCustomProfile && _gridLibrary.SelectedItem is ProfileItem item)
                p.ProfileId = item.Id;

            p.ProfileRotation = _numProfileRot.Value;
            return p;
        }

        private void OnSelectGems(object sender, EventArgs e)
        {
            this.Visible = false;
            try
            {
                var go = new Rhino.Input.Custom.GetObject();
                go.SetCommandPrompt("Select Gems");
                go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh;
                go.GetMultiple(1, 0);

                if (go.CommandResult() == Rhino.Commands.Result.Success)
                {
                    _selectedGems.Clear();
                    foreach (var objRef in go.Objects())
                    {
                        var rhinoObj = objRef.Object();
                        if (rhinoObj == null) continue;

                        // Versuche SmartData zu finden (aus Core/GemSmartData.cs)
                        var data = rhinoObj.Geometry.UserData.Find(typeof(GemSmartData)) as GemSmartData;
                        if (data != null)
                        {
                            _selectedGems.Add(data);
                        }
                        else
                        {
                            // Fallback für "dumme" Geometrie, die wie ein Stein aussieht
                            if (SelectionHelpers.IsGem(rhinoObj))
                            {
                                if (RhinoGoldHelper.TryGetGemData(rhinoObj, out Curve c, out Plane p, out double s))
                                {
                                    _selectedGems.Add(new GemSmartData(c, p, "Unknown", s, "Default", 0));
                                }
                            }
                        }
                    }
                    UpdatePreview();
                }
            }
            finally
            {
                this.Visible = true;
            }
        }

        private void OnBuild(object sender, EventArgs e)
        {
            var p = GetParameters();
            var doc = RhinoDoc.ActiveDoc;

            uint sn = doc.BeginUndoRecord("Create Cutters");

            foreach (var gem in _selectedGems)
            {
                // CutterBuilder (aus NewRhinoGold.BezelStudio) nutzen
                var parts = CutterBuilder.CreateCutter(gem, p);
                if (parts == null) continue;

                foreach (var brep in parts)
                {
                    var attr = doc.CreateDefaultAttributes();

                    // HIER WIRD DER NAME GESETZT
                    attr.Name = "RG Cutter";

                    attr.ObjectColor = System.Drawing.Color.OrangeRed;
                    attr.ColorSource = ObjectColorSource.ColorFromObject;
                    doc.Objects.AddBrep(brep, attr);
                }
            }

            doc.EndUndoRecord(sn);
            doc.Views.Redraw();
            Close(true);
        }

        private void UpdatePreview()
        {
            if (_suspendUpdates) return;
            if (_selectedGems.Count == 0)
            {
                _previewConduit.SetBreps(null);
                RhinoDoc.ActiveDoc.Views.Redraw();
                return;
            }

            var p = GetParameters();
            var previewBreps = new List<Brep>();

            foreach (var gem in _selectedGems)
            {
                var parts = CutterBuilder.CreateCutter(gem, p);
                if (parts != null)
                    previewBreps.AddRange(parts);
            }

            _previewConduit.SetBreps(previewBreps);
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        // --- HELPER ---
        private NumericStepper CreateStepper(double v, int d = 0)
        {
            var s = new NumericStepper { Value = v, DecimalPlaces = d, Width = 60, Font = Eto.Drawing.Fonts.Sans(8) };
            s.ValueChanged += (o, e) => UpdatePreview();
            return s;
        }
    }

    // --- CONDUIT ---
    public class CutterPreviewConduit : Rhino.Display.DisplayConduit
    {
        private List<Brep> _breps;
        private System.Drawing.Color _color = System.Drawing.Color.OrangeRed;

        public void SetBreps(List<Brep> breps)
        {
            _breps = breps;
        }

        protected override void CalculateBoundingBox(Rhino.Display.CalculateBoundingBoxEventArgs e)
        {
            base.CalculateBoundingBox(e);
            if (_breps != null)
            {
                foreach (var b in _breps)
                    e.IncludeBoundingBox(b.GetBoundingBox(false));
            }
        }

        protected override void PostDrawObjects(Rhino.Display.DrawEventArgs e)
        {
            base.PostDrawObjects(e);
            if (_breps != null)
            {
                var mat = new Rhino.Display.DisplayMaterial(_color, 0.6);
                foreach (var b in _breps)
                    e.Display.DrawBrepShaded(b, mat);
            }
        }
    }
}