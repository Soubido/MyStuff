using Eto.Drawing;
using Eto.Forms;
using NewRhinoGold.Core;
using Rhino;
using Rhino.Geometry;
using Rhino.UI;
using System;
using System.Collections.Generic;

namespace NewRhinoGold.Wizard
{
	public class RingWizardDlg : Dialog<bool>
	{
		private RingDesignManager _manager;
		private RingPreviewConduit _conduit;
		private RingPosition _currentEditPos = RingPosition.Top;

		// --- UI Elemente ---

		// Region Section
		private ListBox _listSizes;
		private NumericStepper _numDia;
		private NumericStepper _numCirc;
		private Label _lblRegionName;

		// Front View Section (Die Uhr)
		private Button _btnTop;
		private Button _btnRight;
		private Button _btnBottom;
		private Button _btnLeft;

		// Current Profile Section
		private NumericStepper _numPos;
		private NumericStepper _numRot;
		private NumericStepper _numWidth;
		private NumericStepper _numHeight;
		private Button _btnMirror;

		public RingWizardDlg()
		{
			Title = "Ring Wizard";
			ClientSize = new Size(360, 700);
			Resizable = false;
			Topmost = true;

			_manager = new RingDesignManager();
			_conduit = new RingPreviewConduit();
			_conduit.Enabled = true;

			// Layout erstellen
			Content = BuildMainLayout();

			// Initiale Daten laden
			FillSizeList();
			LoadProfileValues(RingPosition.Top);

			// Events
			Closed += (s, e) => _conduit.Enabled = false;

			// Erstes Update der Vorschau
			UpdateGeometry();
		}

		private Control BuildMainLayout()
		{
			// Haupt-Tabs (Ring Design, Weight, Presets)
			var tabControl = new TabControl();
			var pageDesign = new TabPage { Text = "Ring Design", Content = BuildDesignTab() };
			var pageWeight = new TabPage { Text = "Weight", Content = new Panel() }; // Platzhalter
			var pagePresets = new TabPage { Text = "Presets", Content = new Panel() }; // Platzhalter

			tabControl.Pages.Add(pageDesign);
			tabControl.Pages.Add(pageWeight);
			tabControl.Pages.Add(pagePresets);

			return tabControl;
		}

		private Control BuildDesignTab()
		{
			// Wir nutzen TableLayout für starre Strukturen wie im Screenshot
			var layout = new TableLayout { Padding = 5, Spacing = new Size(5, 5) };

			// 1. OBERER BEREICH: Region & Größen
			var grpRegion = new GroupBox { Text = "Region: Europe" };
			_lblRegionName = new Label { Text = "Standard | Custom" };

			// Flaggen-Buttons (Simuliert durch Text)
			var regionBtns = new TableLayout { Spacing = new Size(2, 0), Rows = { new TableRow(Button("EU"), Button("UK"), Button("US"), Button("JP")) } };

			// Liste links, Werte rechts
			_listSizes = new ListBox { Height = 100 };
			_listSizes.SelectedIndexChanged += (s, e) => UpdateGeometry();

			_numDia = new NumericStepper { DecimalPlaces = 2, ReadOnly = true, Width = 70 };
			_numCirc = new NumericStepper { DecimalPlaces = 2, ReadOnly = true, Width = 70 };

			var sizeInfoLayout = new TableLayout
			{
				Spacing = new Size(5, 5),
				Rows =
				{
					new TableRow(new Label { Text = "Dia." }, _numDia),
					new TableRow(new Label { Text = "Circ." }, _numCirc)
				}
			};

			var splitLayout = new TableLayout
			{
				Spacing = new Size(5, 0),
				Rows = { new TableRow(_listSizes, new TableCell(sizeInfoLayout, true)) }
			};

			var regionContent = new TableLayout { Spacing = new Size(5, 5) };
			regionContent.Rows.Add(new TableRow(_lblRegionName));
			regionContent.Rows.Add(new TableRow(regionBtns));
			regionContent.Rows.Add(new TableRow(splitLayout));
			grpRegion.Content = regionContent;

			layout.Rows.Add(new TableRow(grpRegion));

			// 2. MITTLERER BEREICH: Front View (Die Uhr als Kreuz-Layout)
			var grpFront = new GroupBox { Text = "Front View" };

			var clockLayout = new TableLayout { Padding = 10, Spacing = new Size(2, 2) };

			_btnTop = new Button { Text = "Top", Height = 40 };
			_btnLeft = new Button { Text = "Left", Height = 40 };
			_btnRight = new Button { Text = "Right", Height = 40 };
			_btnBottom = new Button { Text = "Btm", Height = 40 };

			// Events für die Buttons
			_btnTop.Click += (s, e) => LoadProfileValues(RingPosition.Top);
			_btnLeft.Click += (s, e) => LoadProfileValues(RingPosition.Left);
			_btnRight.Click += (s, e) => LoadProfileValues(RingPosition.Right);
			_btnBottom.Click += (s, e) => LoadProfileValues(RingPosition.BottomStart);

			// Anordnung im Gitter
			clockLayout.Rows.Add(new TableRow(null, _btnTop, null));         // 12 Uhr
			clockLayout.Rows.Add(new TableRow(_btnLeft, null, _btnRight));   // 9 und 3 Uhr
			clockLayout.Rows.Add(new TableRow(null, _btnBottom, null));      // 6 Uhr

			grpFront.Content = clockLayout;
			layout.Rows.Add(new TableRow(grpFront));

			// 3. UNTERER BEREICH: Current Profile
			var grpProfile = new GroupBox { Text = "Current Profile" };

			_numPos = new NumericStepper { DecimalPlaces = 2, Width = 60, Enabled = false };
			_numRot = new NumericStepper { DecimalPlaces = 2, Width = 60 };
			_numWidth = new NumericStepper { DecimalPlaces = 2, Width = 60, MinValue = 0.5 };
			_numHeight = new NumericStepper { DecimalPlaces = 2, Width = 60, MinValue = 0.5 };

			// Live Update Events
			_numWidth.ValueChanged += (s, e) => UpdateProfileData();
			_numHeight.ValueChanged += (s, e) => UpdateProfileData();

			_btnMirror = new Button { Text = "Mirror" };
			var btnSolid = new Button { Text = "Solid" };
			var btnDelete = new Button { Text = "Delete" };

			// Grid unten aufbauen
			var profGrid = new TableLayout { Spacing = new Size(5, 5) };

			profGrid.Rows.Add(new TableRow(new Label { Text = "Position" }, _numPos, btnSolid));
			// KORREKTUR HIER: new Label { Text = "..." } statt new Label("...")
			profGrid.Rows.Add(new TableRow(new Label { Text = "Rotation" }, _numRot, new Label { Text = "Thickness" }));
			profGrid.Rows.Add(new TableRow(new Label { Text = "Width" }, _numWidth, _btnMirror));
			profGrid.Rows.Add(new TableRow(new Label { Text = "Height" }, _numHeight, btnDelete));

			grpProfile.Content = profGrid;
			layout.Rows.Add(new TableRow(grpProfile));

			// 4. FOOTER (OK / Cancel)
			var btnOk = new Button { Text = "OK" };
			btnOk.Click += (s, e) => Close(true);
			var btnCancel = new Button { Text = "Cancel" };
			btnCancel.Click += (s, e) => Close(false);

			var footer = new TableLayout { Spacing = new Size(5, 0), Rows = { new TableRow(null, btnOk, btnCancel) } };
			layout.Rows.Add(new TableRow(footer));

			// Spacer am Ende
			layout.Rows.Add(new TableRow { ScaleHeight = true });

			return layout;
		}

		// --- Logik-Methoden ---

		private Button Button(string text) { return new Button { Text = text, Size = new Size(30, 25) }; }

		private void FillSizeList()
		{
			for (int i = 48; i <= 70; i++)
			{
				_listSizes.Items.Add(new ListItem { Text = i.ToString(), Key = i.ToString() });
			}
			_listSizes.SelectedIndex = 6; // Standardauswahl ca 54
		}

		private void LoadProfileValues(RingPosition pos)
		{
			_currentEditPos = pos;
			var sec = _manager.GetSection(pos);

			// Events kurz blockieren
			_numWidth.ValueChanged -= (s, e) => UpdateProfileData();
			_numHeight.ValueChanged -= (s, e) => UpdateProfileData();

			_numWidth.Value = sec.Width;
			_numHeight.Value = sec.Height;
			_numPos.Value = sec.Parameter;

			// Highlight im Button-Text simulieren
			_btnTop.Text = (pos == RingPosition.Top) ? "[ TOP ]" : "Top";
			_btnRight.Text = (pos == RingPosition.Right) ? "[ RIGHT ]" : "Right";
			_btnLeft.Text = (pos == RingPosition.Left) ? "[ LEFT ]" : "Left";
			_btnBottom.Text = (pos == RingPosition.BottomStart) ? "[ BTM ]" : "Btm";

			_numWidth.ValueChanged += (s, e) => UpdateProfileData();
			_numHeight.ValueChanged += (s, e) => UpdateProfileData();
		}

		private void UpdateProfileData()
		{
			_manager.UpdateSection(_currentEditPos, _numWidth.Value, _numHeight.Value);
			UpdateGeometry();
		}

        private void UpdateGeometry()
        {
            if (_listSizes.SelectedValue == null) return;

            // 1. Größe berechnen
            double size = double.Parse(_listSizes.SelectedValue.ToString());
            double diameter = size / Math.PI;
            double radius = diameter / 2.0; // <--- HIER: Radius berechnen

            // UI Update
            _numCirc.Value = size;
            _numDia.Value = diameter;

            // 2. Profile aus dem Manager holen
            // HINWEIS: Stelle sicher, dass dein RingDesignManager eine Methode/Property hat, 
            // die 'RingProfileSlot[]' zurückgibt (z.B. .Sections oder .GetProfileSlots()).
            // Ich nenne sie hier mal 'GetProfileSlots()'.
            var profiles = _manager.GetProfileSlots();

            // 3. 3D Bauen (jetzt mit Radius, Profilen und 'true' für Solid)
            var breps = RingBuilder.BuildRing(radius, profiles, true);

            // Redraw erzwingen
            _conduit.SetGeometry(breps);
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        public Brep[] GetFinalGeometry()
        {
            if (_listSizes.SelectedValue == null) return null;

            // Auch hier müssen wir Radius und Profile erst holen/berechnen:
            double size = double.Parse(_listSizes.SelectedValue.ToString());
            double radius = (size / Math.PI) / 2.0;

            var profiles = _manager.GetProfileSlots();

            // Rückgabe mit 'true' für Solid
            return RingBuilder.BuildRing(radius, profiles, true);
        }
    }
}