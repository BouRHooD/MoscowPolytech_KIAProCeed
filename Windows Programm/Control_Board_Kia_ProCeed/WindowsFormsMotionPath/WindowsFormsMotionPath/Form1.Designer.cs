
namespace WindowsFormsMotionPath
{
    partial class MainForm
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.pictureBoxDraw = new System.Windows.Forms.PictureBox();
            this.labelXY = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.buttonDrawWay = new System.Windows.Forms.Button();
            this.buttonDrawPath = new System.Windows.Forms.Button();
            this.buttonClearPictureBox = new System.Windows.Forms.Button();
            this.buttonDrawVectors = new System.Windows.Forms.Button();
            this.numUpDownCountVectors = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDraw)).BeginInit();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numUpDownCountVectors)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBoxDraw
            // 
            this.pictureBoxDraw.BackColor = System.Drawing.Color.White;
            this.pictureBoxDraw.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxDraw.Location = new System.Drawing.Point(12, 12);
            this.pictureBoxDraw.Name = "pictureBoxDraw";
            this.pictureBoxDraw.Size = new System.Drawing.Size(640, 480);
            this.pictureBoxDraw.TabIndex = 0;
            this.pictureBoxDraw.TabStop = false;
            // 
            // labelXY
            // 
            this.labelXY.AutoSize = true;
            this.labelXY.BackColor = System.Drawing.Color.Transparent;
            this.labelXY.Location = new System.Drawing.Point(20, 470);
            this.labelXY.Name = "labelXY";
            this.labelXY.Size = new System.Drawing.Size(92, 13);
            this.labelXY.TabIndex = 1;
            this.labelXY.Text = "Координаты: X:Y";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.numUpDownCountVectors);
            this.panel1.Controls.Add(this.buttonDrawWay);
            this.panel1.Controls.Add(this.buttonDrawPath);
            this.panel1.Controls.Add(this.buttonClearPictureBox);
            this.panel1.Controls.Add(this.buttonDrawVectors);
            this.panel1.Location = new System.Drawing.Point(658, 12);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(264, 184);
            this.panel1.TabIndex = 73;
            // 
            // buttonDrawWay
            // 
            this.buttonDrawWay.Location = new System.Drawing.Point(5, 77);
            this.buttonDrawWay.Margin = new System.Windows.Forms.Padding(5);
            this.buttonDrawWay.Name = "buttonDrawWay";
            this.buttonDrawWay.Size = new System.Drawing.Size(252, 23);
            this.buttonDrawWay.TabIndex = 70;
            this.buttonDrawWay.Text = "4. Нарисовать траекторию движения";
            this.buttonDrawWay.UseVisualStyleBackColor = true;
            this.buttonDrawWay.Click += new System.EventHandler(this.buttonDrawWay_Click);
            // 
            // buttonDrawPath
            // 
            this.buttonDrawPath.Location = new System.Drawing.Point(5, 53);
            this.buttonDrawPath.Margin = new System.Windows.Forms.Padding(5);
            this.buttonDrawPath.Name = "buttonDrawPath";
            this.buttonDrawPath.Size = new System.Drawing.Size(252, 23);
            this.buttonDrawPath.TabIndex = 69;
            this.buttonDrawPath.Text = "3. Нарисовать траектории";
            this.buttonDrawPath.UseVisualStyleBackColor = true;
            // 
            // buttonClearPictureBox
            // 
            this.buttonClearPictureBox.Location = new System.Drawing.Point(5, 5);
            this.buttonClearPictureBox.Margin = new System.Windows.Forms.Padding(5);
            this.buttonClearPictureBox.Name = "buttonClearPictureBox";
            this.buttonClearPictureBox.Size = new System.Drawing.Size(252, 23);
            this.buttonClearPictureBox.TabIndex = 68;
            this.buttonClearPictureBox.Text = "1. Очистить поле";
            this.buttonClearPictureBox.UseVisualStyleBackColor = true;
            // 
            // buttonDrawVectors
            // 
            this.buttonDrawVectors.Location = new System.Drawing.Point(5, 29);
            this.buttonDrawVectors.Margin = new System.Windows.Forms.Padding(5);
            this.buttonDrawVectors.Name = "buttonDrawVectors";
            this.buttonDrawVectors.Size = new System.Drawing.Size(252, 23);
            this.buttonDrawVectors.TabIndex = 67;
            this.buttonDrawVectors.Text = "2. Нарисовать вектора";
            this.buttonDrawVectors.UseVisualStyleBackColor = true;
            this.buttonDrawVectors.Click += new System.EventHandler(this.buttonDrawVectors_Click);
            // 
            // numUpDownCountVectors
            // 
            this.numUpDownCountVectors.Location = new System.Drawing.Point(5, 121);
            this.numUpDownCountVectors.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numUpDownCountVectors.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.numUpDownCountVectors.Name = "numUpDownCountVectors";
            this.numUpDownCountVectors.Size = new System.Drawing.Size(252, 20);
            this.numUpDownCountVectors.TabIndex = 71;
            this.numUpDownCountVectors.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numUpDownCountVectors.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(74, 105);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(116, 13);
            this.label1.TabIndex = 72;
            this.label1.Text = "Количество векторов";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(934, 501);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.labelXY);
            this.Controls.Add(this.pictureBoxDraw);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximumSize = new System.Drawing.Size(950, 540);
            this.MinimumSize = new System.Drawing.Size(950, 540);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Траектория движения по окружностям и векторам";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDraw)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numUpDownCountVectors)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBoxDraw;
        private System.Windows.Forms.Label labelXY;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button buttonDrawVectors;
        private System.Windows.Forms.Button buttonClearPictureBox;
        private System.Windows.Forms.Button buttonDrawPath;
        private System.Windows.Forms.Button buttonDrawWay;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numUpDownCountVectors;
    }
}

