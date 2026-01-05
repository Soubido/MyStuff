using Eto.Drawing;
using Eto.Forms;
using NewRhinoGold.Core;
using Rhino;
using Rhino.Geometry;
using Rhino.UI;
using Rhino.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRhinoGold.Wizard
{
    // Diese Klasse muss public sein für das Binding
    public class ProfileListItem
    {
        public string Text { get; set; }
        public Image Image { get; set; }
        public string Key => Text;
    }

    public class RingWizardDlg : Dialog<bool>
    {
        private readonly RingDesignManager _manager;
        private readonly RingPreviewConduit _conduit;

        // UI Variablen
        private ListBox _listSizes;
        private NumericStepper _numDia, _numCirc;
        private DropDown _ddMaterials;
        private DropDown _ddProfile;
        private Button _btnPickCurve;

        // Position Buttons
        private Button _btnTop, _btnBottom, _btnLeft, _btnRight;
        private Button _btnTopLeft, _btnTopRight, _btnBottomLeft, _btnBottomRight;

        private NumericStepper _numWidth, _numHeight;
        private CheckBox _chkMirror;

        private RingPosition _currentPos = RingPosition.Top;
        private bool _isUpdatingUI = false;

        public RingWizardDlg()
        {
            Title = "Ring Wizard";
            ClientSize = new Size(380, 720); // Etwas höher für Sicherheit
            Resizable = false;
            Topmost = true;

            _manager = new RingDesignManager();
            _conduit = new RingPreviewConduit();
            _conduit.Enabled = true;

            Content = BuildMainLayout();

            // 1. Listen füllen
            FillSizeList();
            FillMaterialList();
            FillProfileList(); // Hier muss die Liste gefüllt werden

            Closed += (s, e) => _conduit.Enabled = false;

            // 2. Startwerte setzen (Top erzwingen)
            LoadPositionValues(RingPosition.Top);

            // 3. Zeichnen auslösen
            UpdateGeometry();
        }

        private Control BuildMainLayout()
        {
            var layout = new TableLayout { Padding = 10, Spacing = new Size(5, 5) };

            // --- 1. SETTINGS ---
            var grpSettings = new GroupBox { Text = "Settings" };

            _ddMaterials = new DropDown();
            _ddMaterials.SelectedValueChanged += (s, e) => UpdateGeometry();

            _listSizes = new ListBox { Height = 60 };
            _listSizes.SelectedIndexChanged += (s, e) => UpdateGeometry();

            _numDia = new NumericStepper { DecimalPlaces = 2, ReadOnly = true, Width = 60 };
            _numCirc = new NumericStepper { DecimalPlaces = 2, ReadOnly = true, Width = 60 };

            var infoLayout = new TableLayout
            {
                Spacing = new Size(5, 0),
                Rows = { new TableRow("Dia:", _numDia), new TableRow("Cir:", _numCirc) }
            };

            var settingsRows = new TableLayout { Spacing = new Size(5, 5) };
            settingsRows.Rows.Add(new TableRow("Material:", _ddMaterials));
            settingsRows.Rows.Add(new TableRow(_listSizes, new TableCell(infoLayout, true)));
            grpSettings.Content = settingsRows;
            layout.Rows.Add(new TableRow(grpSettings));

            // --- 2. PROFILE MAP (Mitte) ---
            var grpMap = new GroupBox { Text = "Profile Position" };
            var pixelLayout = new PixelLayout { Size = new Size(340, 220) };

            int cx = 170; // Zentrum X
            int cy = 110; // Zentrum Y
            int radius = 75;

            // Dropdown (Oben Mitte)
            _ddProfile = new DropDown { Width = 110 };
            _ddProfile.ItemTextBinding = new PropertyBinding<string>(nameof(ProfileListItem.Text));
            _ddProfile.ItemImageBinding = new PropertyBinding<Image>(nameof(ProfileListItem.Image));
            _ddProfile.SelectedValueChanged += (s, e) => SaveCurrentValues();

            // Pick Button (Direkt darunter)
            _btnPickCurve = new Button { Text = "Pick Crv", Width = 80, Height = 26 };
            _btnPickCurve.Click += OnPickCurveClicked;

            // Positionierung (Manuell Pixelgenau)
            // Dropdown auf y=85 (Mitte - 25)
            pixelLayout.Add(_ddProfile, cx - 55, cy - 25);
            // Button auf y=115 (Mitte + 5)
            pixelLayout.Add(_btnPickCurve, cx - 40, cy + 10);

            // Kreis Buttons
            _btnTop = AddCircularBtn(pixelLayout, "T", RingPosition.Top, cx, cy, radius, -90);
            _btnTopRight = AddCircularBtn(pixelLayout, "TR", RingPosition.TopRight, cx, cy, radius, -45);
            _btnRight = AddCircularBtn(pixelLayout, "R", RingPosition.Right, cx, cy, radius, 0);
            _btnBottomRight = AddCircularBtn(pixelLayout, "BR", RingPosition.BottomRight, cx, cy, radius, 45);
            _btnBottom = AddCircularBtn(pixelLayout, "B", RingPosition.Bottom, cx, cy, radius, 90);
            _btnBottomLeft = AddCircularBtn(pixelLayout, "BL", RingPosition.BottomLeft, cx, cy, radius, 135);
            _btnLeft = AddCircularBtn(pixelLayout, "L", RingPosition.Left, cx, cy, radius, 180);
            _btnTopLeft = AddCircularBtn(pixelLayout, "TL", RingPosition.TopLeft, cx, cy, radius, 225);

            grpMap.Content = pixelLayout;
            layout.Rows.Add(new TableRow(grpMap));

            // --- 3. EDIT VALUES ---
            var grpEdit = new GroupBox { Text = "Dimensions" };
            var editLayout = new TableLayout { Padding = 5, Spacing = new Size(5, 5) };

            _numWidth = new NumericStepper { DecimalPlaces = 2, MinValue = 0.5, MaxValue = 20, Width = 70 };
            _numHeight = new NumericStepper { DecimalPlaces = 2, MinValue = 0.5, MaxValue = 20, Width = 70 };
            _chkMirror = new CheckBox { Text = "Mirror X", Checked = true };

            _numWidth.ValueChanged += (s, e) => SaveCurrentValues();
            _numHeight.ValueChanged += (s, e) => SaveCurrentValues();
            _chkMirror.CheckedChanged += (s, e) => { _manager.MirrorX = _chkMirror.Checked == true; SaveCurrentValues(); };

            editLayout.Rows.Add(new TableRow("Width:", _numWidth, _chkMirror));
            editLayout.Rows.Add(new TableRow("Height:", _numHeight, null));

            grpEdit.Content = editLayout;
            layout.Rows.Add(new TableRow(grpEdit));

            // --- 4. FOOTER ---
            var btnOk = new Button { Text = "OK" };
            btnOk.Click += (s, e) => Close(true);
            var btnCancel = new Button { Text = "Cancel" };
            btnCancel.Click += (s, e) => Close(false);

            layout.Rows.Add(new TableRow { ScaleHeight = true });
            layout.Rows.Add(new TableRow(new TableLayout { Spacing = new Size(5, 0), Rows = { new TableRow(null, btnOk, btnCancel) } }));

            return layout;
        }

        // --- EVENTS ---

        private void OnPickCurveClicked(object sender, EventArgs e)
        {
            this.Visible = false;
            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt("Select Open Profile Curve");
            go.GeometryFilter = Rhino.DocObjects.ObjectType.Curve;
            go.Get();

            if (go.CommandResult() == Rhino.Commands.Result.Success)
            {
                var crv = go.Object(0).Curve();
                if (crv != null)
                {
                    _manager.UpdateSection(_currentPos, _numWidth.Value, _numHeight.Value, crv);

                    _isUpdatingUI = true;
                    _ddProfile.SelectedKey = null;
                    _isUpdatingUI = false;

                    UpdateButtonColors();
                    UpdateGeometry();
                }
            }
            this.Visible = true;
        }

        private Button AddCircularBtn(PixelLayout layout, string text, RingPosition pos, int cx, int cy, int r, double angleDeg)
        {
            var btn = new Button { Text = text, Size = new Size(34, 30) };
            btn.Click += (s, e) => LoadPositionValues(pos);
            btn.Tag = pos;

            // Mathe für Positionierung
            double rad = angleDeg * (Math.PI / 180.0);
            int x = (int)(cx + r * Math.Cos(rad)) - 17; // -Hälfte Breite
            int y = (int)(cy + r * Math.Sin(rad)) - 15; // -Hälfte Höhe

            layout.Add(btn, x, y);
            return btn;
        }

        private void LoadPositionValues(RingPosition pos)
        {
            _isUpdatingUI = true;
            _currentPos = pos;
            var sec = _manager.GetSection(pos);

            _numWidth.Value = sec.Width;
            _numHeight.Value = sec.Height;

            // Versuche Profil im Dropdown zu finden
            bool found = false;
            foreach (var item in _ddProfile.Items)
            {
                if (item is ProfileListItem pli && pli.Text == sec.ProfileName)
                {
                    _ddProfile.SelectedKey = pli.Key;
                    found = true;
                    break;
                }
            }
            if (!found) _ddProfile.SelectedKey = null; // Custom

            UpdateButtonColors();
            _isUpdatingUI = false;
        }

        private void SaveCurrentValues()
        {
            if (_isUpdatingUI) return;

            // Logik: Was ist ausgewählt?
            if (_ddProfile.SelectedValue is ProfileListItem pli)
            {
                // Standard Profil aus Liste
                _manager.UpdateSection(_currentPos, _numWidth.Value, _numHeight.Value, pli.Text);
            }
            else
            {
                // Custom Profil beibehalten oder Default
                var sec = _manager.GetSection(_currentPos);
                if (sec.CustomProfileCurve != null)
                    _manager.UpdateSection(_currentPos, _numWidth.Value, _numHeight.Value, sec.CustomProfileCurve);
                else
                    _manager.UpdateSection(_currentPos, _numWidth.Value, _numHeight.Value, "D-Shape");
            }

            UpdateButtonColors();
            UpdateGeometry(); // Live Update
        }

        private void UpdateButtonColors()
        {
            void SetBtnColor(Button b, RingPosition p)
            {
                var sec = _manager.GetSection(p);
                bool isSelected = (_currentPos == p);
                bool isModified = sec.IsModified;

                if (isSelected) { b.BackgroundColor = Colors.DodgerBlue; b.TextColor = Colors.White; }
                else if (isModified) { b.BackgroundColor = Colors.Orange; b.TextColor = Colors.White; }
                else { b.BackgroundColor = Colors.WhiteSmoke; b.TextColor = Colors.Black; }
            }

            SetBtnColor(_btnTop, RingPosition.Top);
            SetBtnColor(_btnTopLeft, RingPosition.TopLeft);
            SetBtnColor(_btnTopRight, RingPosition.TopRight);
            SetBtnColor(_btnLeft, RingPosition.Left);
            SetBtnColor(_btnRight, RingPosition.Right);
            SetBtnColor(_btnBottom, RingPosition.Bottom);
            SetBtnColor(_btnBottomLeft, RingPosition.BottomLeft);
            SetBtnColor(_btnBottomRight, RingPosition.BottomRight);
        }

        // --- INIT LISTS ---

        private void FillSizeList()
        {
            _listSizes.Items.Clear();
            for (int i = 48; i <= 72; i++)
                _listSizes.Items.Add(new ListItem { Text = i.ToString(), Key = i.ToString() });
            _listSizes.SelectedIndex = 6;
        }

        private void FillMaterialList()
        {
            _ddMaterials.Items.Clear();
            foreach (var mat in Densities.Metals)
                _ddMaterials.Items.Add(new ListItem { Text = mat.Name, Key = mat.Id });

            if (_ddMaterials.Items.Count > 0) _ddMaterials.SelectedIndex = 0;
        }

        private void FillProfileList()
        {
            var items = new List<ProfileListItem>();

            // Abrufen der Namen aus der Library
            var names = RingProfileLibrary.GetProfileNames();

            // Fallback falls leer
            if (names == null || names.Count == 0) names = new List<string> { "D-Shape" };

            foreach (var name in names)
            {
                // Kurve holen für Icon
                var crv = RingProfileLibrary.GetOpenProfile(name);
                var icon = GenerateProfileIcon(crv);
                items.Add(new ProfileListItem { Text = name, Image = icon });
            }

            // WICHTIG: DataStore setzen
            _ddProfile.DataStore = items;

            // Erstes Element wählen
            if (items.Count > 0) _ddProfile.SelectedIndex = 0;
        }

        private Image GenerateProfileIcon(Curve crv)
        {
            if (crv == null) return null;
            try
            {
                int w = 32; int h = 32;
                var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgba);
                using (var g = new Graphics(bmp))
                {
                    // Kurve in Polyline wandeln zum Zeichnen
                    var poly = crv.ToPolyline(0, 0, 0.1, 0, 0, 0, 0, 0, true);
                    if (poly != null && poly.TryGetPolyline(out Rhino.Geometry.Polyline pl))
                    {
                        var bbox = pl.BoundingBox;
                        double width = bbox.Max.X - bbox.Min.X;
                        double height = bbox.Max.Y - bbox.Min.Y;
                        double maxDim = Math.Max(width, height);
                        if (maxDim < 0.001) maxDim = 1;

                        float scale = (float)(22.0 / maxDim);
                        var points = new List<PointF>();
                        float offsetX = w / 2.0f;
                        float offsetY = (h / 2.0f) + 5;

                        foreach (var pt in pl)
                        {
                            float px = offsetX + (float)(pt.X * scale);
                            float py = offsetY - (float)(pt.Y * scale); // Y Flip
                            points.Add(new PointF(px, py));
                        }
                        g.DrawLines(Colors.Black, points.ToArray());
                    }
                }
                return bmp;
            }
            catch
            {
                return null; // Safety
            }
        }

        // --- DRAWING ---

        private void UpdateGeometry()
        {
            if (_listSizes.SelectedValue == null) return;

            RhinoApp.WriteLine("DEBUG: UpdateGeometry aufgerufen.");

            // 1. Werte
            double size = double.Parse(_listSizes.SelectedValue.ToString());
            double radius = (size / Math.PI) / 2.0;
            _numCirc.Value = size;
            _numDia.Value = size / Math.PI;

            // 2. Rail Curve
            var plane = new Plane(Point3d.Origin, Vector3d.XAxis, Vector3d.ZAxis);
            var circle = new Circle(plane, radius);
            circle.Rotate(-Math.PI / 2.0, plane.Normal, plane.Origin);
            Curve rail = circle.ToNurbsCurve();

            // 3. Farbe
            string id = _ddMaterials.SelectedKey;
            var mat = Densities.All.FirstOrDefault(m => m.Id == id);
            var col = mat != null ? mat.DisplayColor : System.Drawing.Color.Gold;

            // 4. Ring Slots
            var slots = _manager.GetProfileSlots();

            // 5. Build
            var ringBreps = RingBuilder.BuildRing(radius, slots, true);

            if (ringBreps != null)
                RhinoApp.WriteLine($"DEBUG: RingBuilder returned {ringBreps.Length} Breps.");
            else
                RhinoApp.WriteLine("DEBUG: RingBuilder returned NULL.");

            // 6. Conduit
            _conduit.SetScene(rail, ringBreps, col);

            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        public Brep[] GetFinalRing()
        {
            if (_listSizes.SelectedValue == null) return null;
            double size = double.Parse(_listSizes.SelectedValue.ToString());
            double radius = (size / Math.PI) / 2.0;
            var slots = _manager.GetProfileSlots();
            return RingBuilder.BuildRing(radius, slots, true);
        }
    }
}