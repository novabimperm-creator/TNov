using System;
using System.Windows.Forms;
using System.Drawing;

namespace TNov
{
    public partial class ConfirmationForm : System.Windows.Forms.Form
    {
        public bool UserConfirmed { get; private set; }

        public ConfirmationForm(string familyAName, string familyBName, int instanceCount, string distance, string levelName = "")
        {
            UserConfirmed = false;
            InitializeForm(familyAName, familyBName, instanceCount, distance, levelName);
        }

        private void InitializeForm(string familyAName, string familyBName, int instanceCount, string distance, string levelName)
        {
            // Основные настройки формы
            this.Text = "Подтверждение операции";
            this.Size = new System.Drawing.Size(500, 400);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(255, 253, 245);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Заголовок
            var titleLabel = new System.Windows.Forms.Label();
            titleLabel.Text = "ПОДТВЕРЖДЕНИЕ РАЗМЕЩЕНИЯ";
            titleLabel.Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
            titleLabel.ForeColor = System.Drawing.Color.FromArgb(193, 115, 0);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(450, 30);
            titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Панель с информацией
            var infoPanel = new System.Windows.Forms.Panel();
            infoPanel.Location = new System.Drawing.Point(20, 70);
            infoPanel.Size = new System.Drawing.Size(450, 200);
            infoPanel.BackColor = System.Drawing.Color.White;
            infoPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

            // Семейство А
            var familyALabel = new System.Windows.Forms.Label();
            familyALabel.Text = "Семейство А (ОВ/ВК):";
            familyALabel.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            familyALabel.ForeColor = System.Drawing.Color.FromArgb(0, 80, 160);
            familyALabel.Location = new System.Drawing.Point(10, 15);
            familyALabel.Size = new System.Drawing.Size(200, 25);
            familyALabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            var familyAValue = new System.Windows.Forms.Label();
            familyAValue.Text = familyAName;
            familyAValue.Font = new System.Drawing.Font("Segoe UI", 10);
            familyAValue.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
            familyAValue.Location = new System.Drawing.Point(220, 15);
            familyAValue.Size = new System.Drawing.Size(220, 25);
            familyAValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Семейство Б
            var familyBLabel = new System.Windows.Forms.Label();
            familyBLabel.Text = "Семейство Б (Электро):";
            familyBLabel.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            familyBLabel.ForeColor = System.Drawing.Color.FromArgb(193, 115, 0);
            familyBLabel.Location = new System.Drawing.Point(10, 50);
            familyBLabel.Size = new System.Drawing.Size(200, 25);
            familyBLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            var familyBValue = new System.Windows.Forms.Label();
            familyBValue.Text = familyBName;
            familyBValue.Font = new System.Drawing.Font("Segoe UI", 10);
            familyBValue.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
            familyBValue.Location = new System.Drawing.Point(220, 50);
            familyBValue.Size = new System.Drawing.Size(220, 25);
            familyBValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Количество
            var countLabel = new System.Windows.Forms.Label();
            countLabel.Text = "Количество на этаже:";
            countLabel.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            countLabel.ForeColor = System.Drawing.Color.FromArgb(0, 100, 0);
            countLabel.Location = new System.Drawing.Point(10, 85);
            countLabel.Size = new System.Drawing.Size(200, 25);
            countLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            var countValue = new System.Windows.Forms.Label();
            countValue.Text = instanceCount.ToString();
            countValue.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            countValue.ForeColor = System.Drawing.Color.FromArgb(0, 100, 0);
            countValue.Location = new System.Drawing.Point(220, 85);
            countValue.Size = new System.Drawing.Size(220, 25);
            countValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Расстояние
            var distanceLabel = new System.Windows.Forms.Label();
            distanceLabel.Text = "Расстояние размещения:";
            distanceLabel.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            distanceLabel.ForeColor = System.Drawing.Color.FromArgb(150, 0, 0);
            distanceLabel.Location = new System.Drawing.Point(10, 120);
            distanceLabel.Size = new System.Drawing.Size(200, 25);
            distanceLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            var distanceValue = new System.Windows.Forms.Label();
            distanceValue.Text = distance;
            distanceValue.Font = new System.Drawing.Font("Segoe UI", 10);
            distanceValue.ForeColor = System.Drawing.Color.FromArgb(150, 0, 0);
            distanceValue.Location = new System.Drawing.Point(220, 120);
            distanceValue.Size = new System.Drawing.Size(220, 25);
            distanceValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Уровень
            var levelLabel = new System.Windows.Forms.Label();
            levelLabel.Text = "Этаж:";
            levelLabel.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            levelLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            levelLabel.Location = new System.Drawing.Point(10, 155);
            levelLabel.Size = new System.Drawing.Size(200, 25);
            levelLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            var levelValue = new System.Windows.Forms.Label();
            levelValue.Text = string.IsNullOrEmpty(levelName) ? "Все этажи" : levelName;
            levelValue.Font = new System.Drawing.Font("Segoe UI", 10);
            levelValue.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            levelValue.Location = new System.Drawing.Point(220, 155);
            levelValue.Size = new System.Drawing.Size(220, 25);
            levelValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            infoPanel.Controls.AddRange(new System.Windows.Forms.Control[] {
                familyALabel, familyAValue, familyBLabel, familyBValue,
                countLabel, countValue, distanceLabel, distanceValue,
                levelLabel, levelValue
            });

            // Предупреждение
            var warningLabel = new System.Windows.Forms.Label();
            warningLabel.Text = "ВНИМАНИЕ: Будет создано новых элементов: " + instanceCount;
            warningLabel.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            warningLabel.ForeColor = System.Drawing.Color.FromArgb(200, 0, 0);
            warningLabel.Location = new System.Drawing.Point(20, 280);
            warningLabel.Size = new System.Drawing.Size(450, 25);
            warningLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Кнопка подтверждения
            var confirmButton = new System.Windows.Forms.Button();
            confirmButton.Text = "ПОДТВЕРДИТЬ РАЗМЕЩЕНИЕ";
            confirmButton.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            confirmButton.Location = new System.Drawing.Point(150, 320);
            confirmButton.Size = new System.Drawing.Size(200, 35);
            confirmButton.BackColor = System.Drawing.Color.FromArgb(40, 167, 69);
            confirmButton.ForeColor = System.Drawing.Color.White;
            confirmButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            confirmButton.Click += (s, e) =>
            {
                UserConfirmed = true;
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            };

            // Кнопка отмены
            var cancelButton = new System.Windows.Forms.Button();
            cancelButton.Text = "ОТМЕНА";
            cancelButton.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            cancelButton.Location = new System.Drawing.Point(370, 320);
            cancelButton.Size = new System.Drawing.Size(100, 35);
            cancelButton.BackColor = System.Drawing.Color.FromArgb(108, 117, 125);
            cancelButton.ForeColor = System.Drawing.Color.White;
            cancelButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cancelButton.Click += (s, e) =>
            {
                UserConfirmed = false;
                this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                this.Close();
            };

            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                titleLabel, infoPanel, warningLabel, confirmButton, cancelButton
            });
        }
    }
}