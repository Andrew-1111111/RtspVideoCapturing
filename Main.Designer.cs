namespace RtspVideoCapturing
{
    partial class Main
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            Button_RunSingleCam = new Button();
            Button_Cancel = new Button();
            Label_VideoFormat = new Label();
            Button_RunAllCams = new Button();
            SuspendLayout();
            // 
            // Button_RunSingleCam
            // 
            Button_RunSingleCam.Location = new Point(12, 12);
            Button_RunSingleCam.Name = "Button_RunSingleCam";
            Button_RunSingleCam.Size = new Size(304, 40);
            Button_RunSingleCam.TabIndex = 0;
            Button_RunSingleCam.Text = "Run Single Cam";
            Button_RunSingleCam.UseVisualStyleBackColor = true;
            Button_RunSingleCam.Click += Button_RunSingleCam_Click;
            // 
            // Button_Cancel
            // 
            Button_Cancel.Location = new Point(12, 104);
            Button_Cancel.Name = "Button_Cancel";
            Button_Cancel.Size = new Size(304, 40);
            Button_Cancel.TabIndex = 1;
            Button_Cancel.Text = "Cancel";
            Button_Cancel.UseVisualStyleBackColor = true;
            Button_Cancel.Click += Button_Cancel_Click;
            // 
            // Label_VideoFormat
            // 
            Label_VideoFormat.AutoSize = true;
            Label_VideoFormat.Location = new Point(13, 147);
            Label_VideoFormat.Name = "Label_VideoFormat";
            Label_VideoFormat.Size = new Size(21, 30);
            Label_VideoFormat.TabIndex = 2;
            Label_VideoFormat.Text = "-";
            // 
            // Button_RunAllCams
            // 
            Button_RunAllCams.Location = new Point(13, 58);
            Button_RunAllCams.Name = "Button_RunAllCams";
            Button_RunAllCams.Size = new Size(304, 40);
            Button_RunAllCams.TabIndex = 3;
            Button_RunAllCams.Text = "Run All Cams";
            Button_RunAllCams.UseVisualStyleBackColor = true;
            Button_RunAllCams.Click += Button_RunAllCams_Click;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(329, 310);
            Controls.Add(Button_RunAllCams);
            Controls.Add(Label_VideoFormat);
            Controls.Add(Button_Cancel);
            Controls.Add(Button_RunSingleCam);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Main";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "RTSP VideoCapturing";
            FormClosing += Main_FormClosing;
            Shown += Main_Shown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button Button_RunSingleCam;
        private Button Button_Cancel;
        private Label Label_VideoFormat;
        private Button Button_RunAllCams;
    }
}
