using System;
using System.Windows.Forms; // Required for System.Windows.Forms.Form and DialogResult
using System.Drawing;       // Required for System.Drawing.Size, Point

namespace Revitron // IMPORTANT: Ensure this namespace matches your project's namespace
{
    // This class provides a simple input dialog using Windows Forms.
    // It inherits from System.Windows.Forms.Form to create a custom window.
    public class PromptWindow : Form
    {
        // Private fields for the UI controls
        private System.Windows.Forms.TextBox textBox;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label label;

        // Public property to get the user's input from the text box
        public string UserPrompt { get; private set; }

        /// <summary>
        /// Constructor for the PromptWindow.
        /// </summary>
        /// <param name="promptText">The text to display as a prompt to the user.</param>
        public PromptWindow(string promptText = "Enter text:")
        {
            // --- Form Setup ---
            this.Text = "Input Required"; // Title of the window
            this.FormBorderStyle = FormBorderStyle.FixedDialog; // Non-resizable dialog
            this.StartPosition = FormStartPosition.CenterScreen; // Center on screen
            this.MinimizeBox = false; // No minimize button
            this.MaximizeBox = false; // No maximize button
            this.AcceptButton = okButton; // Enter key presses OK
            this.CancelButton = cancelButton; // Escape key presses Cancel
            this.ClientSize = new System.Drawing.Size(300, 120); // Fixed size for the client area

            // --- Label Control ---
            label = new System.Windows.Forms.Label();
            label.Text = promptText;
            label.Location = new System.Drawing.Point(10, 10); // Position relative to form
            label.AutoSize = true; // Adjusts size automatically based on text
            this.Controls.Add(label); // Add label to the form's controls

            // --- Text Box Control ---
            textBox = new System.Windows.Forms.TextBox();
            textBox.Location = new System.Drawing.Point(10, 35);
            textBox.Size = new System.Drawing.Size(280, 20);
            this.Controls.Add(textBox);

            // --- OK Button Control ---
            okButton = new System.Windows.Forms.Button();
            okButton.Text = "OK";
            okButton.Location = new System.Drawing.Point(130, 70);
            okButton.DialogResult = DialogResult.OK; // Sets the dialog result when clicked
            // Event handler for OK button click
            okButton.Click += (sender, e) => { UserPrompt = textBox.Text; this.Close(); };
            this.Controls.Add(okButton);

            // --- Cancel Button Control ---
            cancelButton = new System.Windows.Forms.Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new System.Drawing.Point(215, 70);
            cancelButton.DialogResult = DialogResult.Cancel; // Sets the dialog result when clicked
            // Event handler for Cancel button click
            cancelButton.Click += (sender, e) => { this.Close(); };
            this.Controls.Add(cancelButton);
        }

        /// <summary>
        /// Overrides the base ShowDialog method to ensure it's called correctly.
        /// </summary>
        public new DialogResult ShowDialog()
        {
            return base.ShowDialog();
        }
    }
}
