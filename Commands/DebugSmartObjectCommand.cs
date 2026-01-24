using System;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using Eto.Forms;
using Eto.Drawing;
using NewRhinoGold.Core;

namespace NewRhinoGold.Commands
{
    [Guid("8F41A2D9-3C5B-4E7F-9A1D-2B8E5C4F6A9D")]
    public class DebugSmartObjectCommand : Rhino.Commands.Command
    {
        public DebugSmartObjectCommand()
        {
            Instance = this;
        }

        public static DebugSmartObjectCommand Instance { get; private set; }

        public override string EnglishName => "DebugSmartObject";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select Smart Object to Inspect");
            go.GeometryFilter = ObjectType.AnyObject;
            go.SubObjectSelect = false;
            go.Get();

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var objRef = go.Object(0);
            var obj = objRef.Object();
            if (obj == null) return Result.Failure;

            var report = new StringBuilder();
            report.AppendLine($"--- INSPECTION REPORT FOR OBJECT {obj.Id} ---");
            report.AppendLine($"Type: {obj.ObjectType}");
            report.AppendLine($"Name: {obj.Name ?? "<Unnamed>"}");
            report.AppendLine();

            var geomUserData = obj.Geometry?.UserData;
            var attrUserData = obj.Attributes?.UserData;

            bool foundData = false;

            // Lokale Funktion zur sicheren Analyse
            void AnalyzeDataList(Rhino.DocObjects.Custom.UserDataList list, string source)
            {
                if (list == null || list.Count == 0) return;

                foreach (var data in list)
                {
                    // SICHERHEITS-CHECK 1: Daten-Objekt selbst kann null sein
                    if (data == null)
                    {
                        report.AppendLine($"[WARNING] Found NULL entry in {source}");
                        continue;
                    }

                    foundData = true;

                    try
                    {
                        report.AppendLine(new string('=', 40));
                        report.AppendLine($"SOURCE: {source}");
                        // SICHERHEITS-CHECK 2: Safe Access Operatoren
                        report.AppendLine($"DATA TYPE: {data.GetType()?.FullName ?? "Unknown Type"}");
                        report.AppendLine($"Description: {data.Description ?? "No Description"}");

                        // Versuchen, ShouldWrite zu lesen (kann bei defekten Objekten fehlschlagen)
                        string writeStatus = "Unknown";
                        try { writeStatus = data.ShouldWrite.ToString().ToUpper(); } catch { }
                        report.AppendLine($"ShouldWrite: {writeStatus} (Must be TRUE for save)");

                        report.AppendLine(new string('-', 40));

                        // Inhalt via Reflection auslesen
                        DumpProperties(data, report, "");
                    }
                    catch (Exception ex)
                    {
                        report.AppendLine($"[ERROR inspecting data item]: {ex.Message}");
                    }
                }
            }

            // Listen analysieren
            AnalyzeDataList(geomUserData, "Geometry.UserData (Brep/Mesh)");
            AnalyzeDataList(attrUserData, "Attributes.UserData (Object)");

            if (!foundData)
            {
                report.AppendLine(">> NO USER DATA FOUND <<");
                report.AppendLine("Diagnostic:");
                report.AppendLine("1. Check if 'RingSmartData.cs' has 'override bool ShouldWrite => true;'");
                report.AppendLine("2. Ensure data was added via 'geometry.UserData.Add()'");
                report.AppendLine("3. If this is an old object, the data might be lost or format incompatible.");
            }

            // Dialog anzeigen
            var dlg = new DebugInfoDlg(report.ToString());
            dlg.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);

            return Result.Success;
        }

        private void DumpProperties(object obj, StringBuilder sb, string indent)
        {
            if (obj == null) return;
            if (indent.Length > 20) return; // Rekursionsbremse

            if (obj is IEnumerable enumerable && !(obj is string))
            {
                try
                {
                    int i = 0;
                    foreach (var item in enumerable)
                    {
                        sb.AppendLine($"{indent}[{i}]");
                        DumpProperties(item, sb, indent + "  ");
                        i++;
                    }
                }
                catch (Exception ex) { sb.AppendLine($"{indent}[Error iterating list]: {ex.Message}"); }
                return;
            }

            // Eigenschaften auslesen
            var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (p.Name == "Description" || p.Name == "ShouldWrite") continue;

                try
                {
                    var val = p.GetValue(obj);

                    if (val == null)
                    {
                        sb.AppendLine($"{indent}{p.Name}: [NULL]");
                    }
                    else if (val is Rhino.Geometry.Curve crv)
                    {
                        sb.AppendLine($"{indent}{p.Name}: [CURVE] Valid={crv.IsValid}, Len={crv.GetLength():F2}");
                    }
                    else if (val.GetType().IsPrimitive || val is string || val is Guid || val is Enum || val is Rhino.Geometry.Point3d || val is Rhino.Geometry.Vector3d || val is int || val is double || val is bool)
                    {
                        sb.AppendLine($"{indent}{p.Name}: {val}");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}{p.Name}: <{val.GetType().Name}>");
                        DumpProperties(val, sb, indent + "  ");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"{indent}{p.Name}: [Error reading prop] {ex.Message}");
                }
            }
        }
    }

    public class DebugInfoDlg : Dialog<DialogResult>
    {
        public DebugInfoDlg(string reportText)
        {
            Title = "SmartData Inspector";
            ClientSize = new Size(600, 700);
            Resizable = true;
            Topmost = true;

            var textArea = new TextArea
            {
                ReadOnly = true,
                Text = reportText,
                Font = Eto.Drawing.Fonts.Monospace(9),
                Wrap = false
            };

            var btnClose = new Button { Text = "Close" };
            btnClose.Click += (s, e) => Close();

            var layout = new TableLayout
            {
                Padding = 10,
                Spacing = new Size(5, 5),
                Rows =
                {
                    new TableRow(textArea) { ScaleHeight = true },
                    new TableRow(btnClose)
                }
            };

            Content = layout;
        }
    }
}