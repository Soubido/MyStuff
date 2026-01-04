using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.UI;

namespace NewRhinoGold.Dialog
{
	public class GoldWeightDlg : Eto.Forms.Dialog<DialogResult>
	{
		private DropDown _materialDropdown;
		private Label _weightLabel;
		private Button _btnSelect;
		private Button _btnClose;

		public GoldWeightDlg()
		{
			Title = "Metall Gewicht";
			Resizable = true;
			ClientSize = new Size(150, 200);
			Topmost = true;

			Content = BuildLayout();
		}

		private Control BuildLayout()
		{
			_btnSelect = new Button { Text = "Objekt wählen" };
			_btnSelect.Click += OnSelectClick;

			_materialDropdown = new DropDown();
			// Lade nur Metalle aus Densities
			if (Densities.Metals != null)
			{
				foreach (var metal in Densities.Metals.OrderBy(m => m.Name))
				{
					_materialDropdown.Items.Add(metal.Name);
				}
			}
			if (_materialDropdown.Items.Count > 0)
				_materialDropdown.SelectedIndex = 0; // Default

			_weightLabel = new Label { Text = "Gewicht: -" };

			// FIX CS0104: Expliziter Namespace Eto.Drawing.Font
			_weightLabel.Font = new Eto.Drawing.Font(_weightLabel.Font.Family, _weightLabel.Font.Size, Eto.Drawing.FontStyle.Bold);

			_btnClose = new Button { Text = "Schließen" };
			_btnClose.Click += (s, e) => Close(DialogResult.Ok);

			var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };

			layout.AddRow(_btnSelect);
			layout.AddRow(null); // Spacer
			layout.AddRow(new Label { Text = "Bitte Metall wählen:" });
			layout.AddRow(_materialDropdown);
			layout.AddRow(new Label { Text = "Gewicht abzüglich 5%:" });
			layout.AddRow(_weightLabel);
			layout.AddRow(null); // Spacer
			layout.AddRow(_btnClose);

			return layout;
		}

		private void OnSelectClick(object sender, EventArgs e)
		{
			Visible = false;

			var rc = RhinoGet.GetMultipleObjects("Bitte wählen Sie ein Objekt aus", false, ObjectType.Surface | ObjectType.PolysrfFilter | ObjectType.Mesh, out var objRefs);

			if (rc != Rhino.Commands.Result.Success || objRefs == null)
			{
				Visible = true;
				return;
			}

			double totalVolume = 0;
			bool errors = false;

			foreach (var objRef in objRefs)
			{
				var geometry = objRef.Geometry();
				if (geometry == null) continue;

				if (geometry is Mesh mesh)
				{
					if (!mesh.IsClosed)
					{
						MessageBox.Show("Das ausgewählte Objekt ist kein geschlossenes Mesh.", "Offenes Mesh", MessageBoxType.Warning);
						errors = true;
						continue;
					}
					var mp = VolumeMassProperties.Compute(mesh);
					if (mp != null) totalVolume += Math.Abs(mp.Volume);
				}
				else if (geometry is Brep brep)
				{
					if (!brep.IsSolid)
					{
						MessageBox.Show("Das ausgewählte Objekt ist kein geschlossener Volumenkörper (Brep).", "Offenes Brep", MessageBoxType.Warning);
						errors = true;
						continue;
					}
					var mp = VolumeMassProperties.Compute(brep);
					if (mp != null) totalVolume += Math.Abs(mp.Volume);
				}
			}

			if (!errors || totalVolume > 0)
			{
				double volumeReduced = totalVolume * 0.95;

				string matName = _materialDropdown.SelectedValue?.ToString();
				double density = 0;

				if (!string.IsNullOrEmpty(matName))
				{
					density = Densities.GetDensity(matName);
				}

				double weight = volumeReduced * density / 1000.0;

				_weightLabel.Text = $"Gewicht: {weight:F2}g";
			}

			Visible = true;
		}
	}
}