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
using System.Windows.Forms;
using Microsoft.Scripting.Hosting;
using System.Collections.Generic;

namespace Revitron
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
            System.Windows.Forms.DialogResult dialogResult = promptWindow.ShowDialog();

            if (dialogResult != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(promptWindow.UserPrompt))
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
                TaskDialog.Show("Execution Error", "Failed to start Python process. Ensure a standalone Python is installed and added to your system's PATH. Error: " + ex.Message);
                return Result.Failed;
            }

            // --- IMMEDIATE C# DEBUGGING STEP ---
            TaskDialog.Show("C# Debug", "Python process completed. Checking outputs now.");
            // --- END IMMEDIATE C# DEBUGGING STEP ---

            // --- DEBUGGING STDERR CONTENT ---
            // Display actual errors from Python's stderr if not empty.
            // We are temporarily removing the "StartsWith('INFO')" check and the immediate return here
            // so we can see the generated code and subsequent IronPython errors.
            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                TaskDialog.Show("Python Error (from STDERR)", errorOutput);
                // Removed: return Result.Failed;
            }
            // --- END DEBUGGING STDERR CONTENT ---

            // Check if the Python script returned any code
            if (string.IsNullOrWhiteSpace(generatedCode))
            {
                TaskDialog.Show("No Code Returned", "Python script did not output any code to stdout. Check rag_debug.log for details.");
                return Result.Failed;
            }

            // --- DEBUGGING STEP: Show the raw output from Python's stdout ---
            TaskDialog.Show("Python Script STDOUT (Generated Code)",
                            generatedCode);
            // --- END DEBUGGING STEP ---

            try
            {
                var engine = IronPython.Hosting.Python.CreateEngine();

                engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.DB.Document).Assembly);
                engine.Runtime.LoadAssembly(typeof(Autodesk.Revit.UI.TaskDialog).Assembly);

                string revitApiPath = Path.GetDirectoryName(typeof(Autodesk.Revit.DB.Document).Assembly.Location);
                string revitApiUIPath = Path.GetDirectoryName(typeof(Autodesk.Revit.UI.TaskDialog).Assembly.Location);

                var paths = new List<string>(engine.GetSearchPaths());
                if (!paths.Contains(revitApiPath)) paths.Add(revitApiPath);
                if (!paths.Contains(revitApiUIPath)) paths.Add(revitApiUIPath);
                engine.SetSearchPaths(paths);

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
                TaskDialog.Show("IronPython Execution Error", "Error executing generated Python code: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}
