using System;
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;

namespace NewRhinoGold.Dialog
{
    [System.Runtime.InteropServices.Guid("D93808C6-2313-4315-B667-89735E263888")]
    public class MainToolbarDlg : Panel
    {
        public static Guid PanelId => typeof(MainToolbarDlg).GUID;

        private readonly Eto.Drawing.Font _fontHeader = Eto.Drawing.Fonts.Sans(9, FontStyle.Bold);
        private readonly Eto.Drawing.Font _fontBtn = Eto.Drawing.Fonts.Sans(9);

        private readonly Size _btnSize = new Size(-1, 32);
        private readonly Padding _padding = new Padding(5);

        public MainToolbarDlg()
        {
            Content = BuildLayout();
        }

        private Control BuildLayout()
        {
            var layout = new DynamicLayout { Padding = _padding, Spacing = new Size(0, 5) };

            layout.BeginVertical();



            // --- CREATION ---
            layout.Add(CreateHeader("Creation"));
            // Hier wird jetzt RingWizard.png geladen
            layout.Add(CreateCmdBtn("Ring Wizard", "_RingWizard", "RingWizard.png"));

            layout.Add(CreateCmdBtn("Gem Studio", "_GemStudio", "GemStudio.png"));
            layout.Add(CreateCmdBtn("Gem Creator", "_GemCreator", "GemCreator.png"));
            layout.Add(CreateCmdBtn("Cutter Studio", "_CutterStudio", "CutterStudio.png"));

            // Falls Sie für BezelStudio kein Icon haben, lassen wir es leer oder nutzen ein Platzhalter
            layout.Add(CreateCmdBtn("Bezel Studio", "_BezelStudio.", "BezelStudio.png"));

            layout.Add(CreateCmdBtn("Head Studio", "_HeadStudio", "HeadStudio.png"));
            layout.Add(CreateCmdBtn("Pave Studio", "_PaveStudio", "PaveStudio.png"));
            layout.Add(null);

            // --- EDIT ---
            layout.Add(CreateHeader("Edit"));
            layout.Add(CreateCmdBtn("Edit Smart Object", "_EditSmartObject"));
            layout.Add(null);

            // --- TOOLS ---
            layout.Add(CreateHeader("Tools"));
            layout.Add(CreateCmdBtn("Extract Curve", "_ExtractGemCurve", "ExtractGemCrv.png"));
            layout.Add(CreateCmdBtn("Move on Srf", "_MoveOnSurface", "MoveOnSurface.png"));
            layout.Add(CreateCmdBtn("Engraving Builder", "_EngraveRing", "EngraveStudio.png"));
            layout.Add(null);

            // --- ANALYSIS ---
            layout.Add(CreateHeader("Analysis"));
            layout.Add(CreateCmdBtn("Gold Weight", "_GoldWeight", "GoldWeight.png"));
            layout.Add(CreateCmdBtn("Gem Report", "_GemReport", "GemReport.png"));

            // --- SELECTION ---
            layout.Add(CreateHeader("Selection"));
            // Hier wird jetzt überall "Selection.png" verwendet, wie gewünscht
            layout.Add(CreateCmdBtn("Select Gems", "_SelGems", "Selection.png"));
            layout.Add(CreateCmdBtn("Select Heads", "_SelHeads", "Selection.png"));
            layout.Add(CreateCmdBtn("Select Bezels", "_SelBezels", "Selection.png"));
            layout.Add(CreateCmdBtn("Select Cutters", "_SelCutters", "Selection.png"));
            layout.Add(null);

            layout.Add(new Panel { Height = 10 });
            layout.EndVertical();

            return new Scrollable { Content = layout, Border = BorderType.None };
        }

        private Control CreateHeader(string text)
        {
            return new Label
            {
                Text = text,
                Font = _fontHeader,
                TextColor = Colors.Gray,
                TextAlignment = TextAlignment.Center
            };
        }

        private Button CreateCmdBtn(string label, string command, string iconName = null)
        {
            var btn = new Button
            {
                Text = label,
                Font = _fontBtn,
                Size = _btnSize
            };

            if (!string.IsNullOrEmpty(iconName))
            {
                var icon = LoadIcon(iconName);
                if (icon != null)
                {
                    btn.Image = icon;
                    btn.ImagePosition = ButtonImagePosition.Left;
                }
            }

            btn.Click += (s, e) => RhinoApp.RunScript(command, false);
            return btn;
        }

        private Image LoadIcon(string name)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                // Stellen Sie sicher, dass 'NewRhinoGold' Ihr korrekter Namespace ist!
                string resourceName = $"NewRhinoGold.Icons.{name}";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        return new Bitmap(stream);
                    }
                }
            }
            catch
            {
                // Fehlerunterdrückung, falls Icon fehlt
            }
            return null;
        }
    }
}