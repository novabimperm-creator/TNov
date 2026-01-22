using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using System.Windows.Forms;
using System.Drawing;

namespace TNov
{
    public class ResultForm : System.Windows.Forms.Form
    {
        public ResultForm(List<ElementId> createdElements)
        {
            InitializeForm(createdElements);
        }

        private void InitializeForm(List<ElementId> createdElements)
        {
            bool success = createdElements != null && createdElements.Count > 0;

            // Основные настройки формы - ЯВНО указываем пространства имен
            this.Text = "Результат размещения";
            this.Size = new System.Drawing.Size(700, 400);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = success ?
                System.Drawing.Color.FromArgb(240, 255, 240) :
                System.Drawing.Color.FromArgb(255, 240, 240);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Заголовок
            var titleLabel = new System.Windows.Forms.Label();
            titleLabel.Text = success ? "РАЗМЕЩЕНИЕ УСПЕШНО" : "РАЗМЕЩЕНИЕ НЕ ВЫПОЛНЕНО";
            titleLabel.Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
            titleLabel.ForeColor = success ?
                System.Drawing.Color.FromArgb(0, 100, 0) :
                System.Drawing.Color.FromArgb(220, 53, 69);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(650, 30);
            titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Сообщение
            var messageLabel = new System.Windows.Forms.Label();
            if (success)
            {
                messageLabel.Text = $"Успешно размещено {createdElements.Count} элементов";
            }
            else
            {
                messageLabel.Text = "Элементы не были размещены";
            }
            messageLabel.Font = new System.Drawing.Font("Segoe UI", 10);
            messageLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            messageLabel.Location = new System.Drawing.Point(20, 60);
            messageLabel.Size = new System.Drawing.Size(650, 25);
            messageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.Controls.Add(messageLabel);

            // Список ID
            if (success)
            {
                var listLabel = new System.Windows.Forms.Label();
                listLabel.Text = $"ID размещенных элементов:";
                listLabel.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
                listLabel.ForeColor = System.Drawing.Color.FromArgb(0, 80, 160);
                listLabel.Location = new System.Drawing.Point(20, 90);
                listLabel.Size = new System.Drawing.Size(650, 25);
                listLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                this.Controls.Add(listLabel);

                var listBox = new System.Windows.Forms.ListBox();
                listBox.Location = new System.Drawing.Point(20, 120);
                listBox.Size = new System.Drawing.Size(650, 200);
                listBox.Font = new System.Drawing.Font("Consolas", 9);
                listBox.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;

                foreach (var elementId in createdElements)
                {
                    listBox.Items.Add($"ID: {elementId.IntegerValue}");
                }

                this.Controls.Add(listBox);
            }

            // Кнопка OK
            var okButton = new System.Windows.Forms.Button();
            okButton.Text = "OK";
            okButton.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            okButton.Location = new System.Drawing.Point(300, 330);
            okButton.Size = new System.Drawing.Size(100, 35);
            okButton.BackColor = System.Drawing.Color.FromArgb(0, 123, 255);
            okButton.ForeColor = System.Drawing.Color.White;
            okButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            okButton.DialogResult = System.Windows.Forms.DialogResult.OK;

            this.Controls.Add(okButton);
            this.AcceptButton = okButton;
        }
    }
}