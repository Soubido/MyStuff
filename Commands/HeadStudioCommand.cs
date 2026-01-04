using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using NewRhinoGold.Studio;

namespace NewRhinoGold.Commands
{
    public class HeadStudioCommand : Command
    {
        public override string EnglishName => "HeadStudio";
        public override Guid Id => new Guid("F1234567-89AB-CDEF-0123-456789ABCDEF");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                var dlg = new HeadStudioDlg();

                // Sicherer Zugriff auf das Owner-Fenster
                var owner = RhinoEtoApp.MainWindow;
                if (owner == null && Eto.Forms.Application.Instance != null)
                {
                    owner = Eto.Forms.Application.Instance.MainForm;
                }
                dlg.Owner = owner;

                dlg.Show();
                return Result.Success;
            }
            catch (Exception ex)
            {
                // ZEIGT DEN FEHLER AN, statt ihn zu verschweigen
                RhinoApp.WriteLine($"Fehler beim Starten des Dialogs: {ex.Message}");
                Eto.Forms.MessageBox.Show($"Fehler: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Head Studio Error", Eto.Forms.MessageBoxType.Error);
                return Result.Failure;
            }
        }
    }
}