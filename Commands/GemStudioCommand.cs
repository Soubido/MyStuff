using Rhino;
using Rhino.Commands;
using Rhino.UI;
using System;

namespace NewRhinoGold.Commands
{
    public class GemStudioCommand : Command
    {
        // Statische Referenz zum Speichern der offenen Instanz
        private static NewRhinoGold.Studio.GemStudioDlg _instance;

        public override string EnglishName => "GemStudio";

        public override Guid Id => new Guid("8C5236A3-F428-4981-8056-111122223333");

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Prüfung: Ist der Dialog bereits offen?
            if (_instance != null)
            {
                _instance.BringToFront();
                return Result.Success;
            }

            // Neue Instanz erstellen
            _instance = new NewRhinoGold.Studio.GemStudioDlg();

            // Event-Handler: Referenz löschen, wenn Dialog geschlossen wird
            _instance.Closed += (sender, e) =>
            {
                _instance = null;
            };

            // KORREKTUR: Owner setzen und Show() verwenden statt ShowSemiModal
            _instance.Owner = Rhino.UI.RhinoEtoApp.MainWindow;
            _instance.Show();

            return Result.Success;
        }
    }
}