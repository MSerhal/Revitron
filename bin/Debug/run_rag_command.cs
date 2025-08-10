// RunRAGCommand.cs
using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using IronPython.Hosting;

namespace RevitCopilot
{
    [Transaction(TransactionMode.Manual)]
    public class RunRAGCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            var promptWindow = new PromptWindow();
            bool? result = promptWindow.ShowDialog();
            if (result != true || string.IsNullOrWhiteSpace(promptWindow.UserPrompt))
                return Result.Cancelled;

            string userQuery = promptWindow.UserPrompt;

            string asmDir = Path.GetDirectoryName(
                typeof(RunRAGCommand).Assembly.Location);
            string pyScript = Path.Combine(asmDir, "python", "generate_rag_prompt.py");

            if (!File.Exists(pyScript))
            {
                TaskDialog.Show("Error", $"Could not find Python script at:\n{pyScript}");
                return Result.Failed;
            }

            string generatedCode = null;
            string errorOutput = null;

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{pyScript}\" \"{userQuery}\"",
                WorkingDirectory = Path.GetDirectoryName(pyScript),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using var proc = Process.Start(psi);
                generatedCode = proc.StandardOutput.ReadToEnd();
                errorOutput = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Execution Error", ex.Message);
                return Result.Failed;
            }

            if (!string.IsNullOrWhiteSpace(errorOutput) && !errorOutput.TrimStart().StartsWith("INFO"))
            {
                TaskDialog.Show("Python Error", errorOutput);
                return Result.Failed;
            }

            if (string.IsNullOrWhiteSpace(generatedCode))
            {
                TaskDialog.Show("No Code Returned", "Python script did not output anything.");
                return Result.Failed;
            }

            try
            {
                var engine = IronPython.Hosting.Python.CreateEngine();
                var scope = engine.CreateScope();

                scope.SetVariable("doc", commandData.Application.ActiveUIDocument.Document);
                scope.SetVariable("uidoc", commandData.Application.ActiveUIDocument);
                scope.SetVariable("uiapp", commandData.Application);
                scope.SetVariable("app", commandData.Application.Application);

                var source = engine.CreateScriptSourceFromString(
                    generatedCode,
                    Microsoft.Scripting.SourceCodeKind.Statements);

                using (Transaction tx = new Transaction(commandData.Application.ActiveUIDocument.Document, "Run Copilot Script"))
                {
                    tx.Start();
                    source.Execute(scope);
                    tx.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Execution Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
