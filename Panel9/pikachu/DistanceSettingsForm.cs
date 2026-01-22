using Autodesk.Revit.DB;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace TNov
{
    public partial class DistanceSettingsForm : System.Windows.Forms.Form
    {
        public double Distance { get; private set; }
        public XYZ Direction { get; private set; }

        private System.Windows.Forms.RadioButton _defaultDistanceRadio;
        private System.Windows.Forms.RadioButton _customDistanceRadio;
        private System.Windows.Forms.NumericUpDown _distanceNumeric;
        private System.Windows.Forms.ComboBox _unitsComboBox;
        private System.Windows.Forms.ComboBox _directionComboBox;

        public DistanceSettingsForm()
        {
            Distance = 0.5; // значение по умолчанию
            Direction = XYZ.BasisZ; // По умолчанию смещение вверх
            InitializeForm();
        }

        private void InitializeForm()
        {
            // Основные настройки формы
            this.Text = "Настройка расстояния размещения";
            this.Size = new System.Drawing.Size(550, 350); // Увеличено для направления
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(255, 250, 245);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Заголовок
            var titleLabel = new System.Windows.Forms.Label();
            titleLabel.Text = "НАСТРОЙКА РАССТОЯНИЯ РАЗМЕЩЕНИЯ";
            titleLabel.Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
            titleLabel.ForeColor = System.Drawing.Color.FromArgb(193, 115, 0);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(500, 30);
            titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Описание
            var descLabel = new System.Windows.Forms.Label();
            descLabel.Text = "Настройте расстояние и направление между выбранными семействами:";
            descLabel.Font = new System.Drawing.Font("Segoe UI", 10);
            descLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            descLabel.Location = new System.Drawing.Point(20, 60);
            descLabel.Size = new System.Drawing.Size(500, 25);
            descLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Группа настроек расстояния
            var distanceGroup = new System.Windows.Forms.GroupBox();
            distanceGroup.Text = "Параметры размещения";
            distanceGroup.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            distanceGroup.ForeColor = System.Drawing.Color.FromArgb(0, 80, 160);
            distanceGroup.Location = new System.Drawing.Point(20, 95);
            distanceGroup.Size = new System.Drawing.Size(500, 150);
            distanceGroup.BackColor = System.Drawing.Color.FromArgb(240, 245, 255);

            // Опция по умолчанию
            _defaultDistanceRadio = new System.Windows.Forms.RadioButton();
            _defaultDistanceRadio.Text = "Разместить рядом (смещение 0.5 м по вертикали вверх)";
            _defaultDistanceRadio.Font = new System.Drawing.Font("Segoe UI", 9);
            _defaultDistanceRadio.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
            _defaultDistanceRadio.Location = new System.Drawing.Point(15, 25);
            _defaultDistanceRadio.Size = new System.Drawing.Size(470, 25);
            _defaultDistanceRadio.Checked = true;
            _defaultDistanceRadio.CheckedChanged += (s, e) => UpdateControlsState();

            // Опция пользовательского расстояния
            _customDistanceRadio = new System.Windows.Forms.RadioButton();
            _customDistanceRadio.Text = "Задать свое расстояние:";
            _customDistanceRadio.Font = new System.Drawing.Font("Segoe UI", 9);
            _customDistanceRadio.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
            _customDistanceRadio.Location = new System.Drawing.Point(15, 55);
            _customDistanceRadio.Size = new System.Drawing.Size(200, 25);
            _customDistanceRadio.CheckedChanged += (s, e) => UpdateControlsState();

            // Числовое поле для расстояния
            _distanceNumeric = new System.Windows.Forms.NumericUpDown();
            _distanceNumeric.Location = new System.Drawing.Point(15, 85);
            _distanceNumeric.Size = new System.Drawing.Size(100, 23);
            _distanceNumeric.Font = new System.Drawing.Font("Segoe UI", 9);
            _distanceNumeric.DecimalPlaces = 2;
            _distanceNumeric.Minimum = -100;
            _distanceNumeric.Maximum = 100;
            _distanceNumeric.Value = (decimal)Distance;
            _distanceNumeric.Increment = 0.1M;
            _distanceNumeric.ValueChanged += (s, e) => Distance = (double)_distanceNumeric.Value;

            // Выбор единиц измерения
            _unitsComboBox = new System.Windows.Forms.ComboBox();
            _unitsComboBox.Location = new System.Drawing.Point(125, 85);
            _unitsComboBox.Size = new System.Drawing.Size(100, 23);
            _unitsComboBox.Font = new System.Drawing.Font("Segoe UI", 9);
            _unitsComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            _unitsComboBox.Items.AddRange(new object[] { "метры", "миллиметры" });
            _unitsComboBox.SelectedIndex = 0;
            _unitsComboBox.SelectedIndexChanged += (s, e) => UpdateDistanceUnits();

            // Подпись к полю расстояния
            var distanceLabel = new System.Windows.Forms.Label();
            distanceLabel.Text = "Расстояние:";
            distanceLabel.Font = new System.Drawing.Font("Segoe UI", 9);
            distanceLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            distanceLabel.Location = new System.Drawing.Point(235, 88);
            distanceLabel.Size = new System.Drawing.Size(80, 20);
            distanceLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Выбор направления
            var directionLabel = new System.Windows.Forms.Label();
            directionLabel.Text = "Направление:";
            directionLabel.Font = new System.Drawing.Font("Segoe UI", 9);
            directionLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            directionLabel.Location = new System.Drawing.Point(15, 115);
            directionLabel.Size = new System.Drawing.Size(100, 20);
            directionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            _directionComboBox = new System.Windows.Forms.ComboBox();
            _directionComboBox.Location = new System.Drawing.Point(125, 115);
            _directionComboBox.Size = new System.Drawing.Size(200, 23);
            _directionComboBox.Font = new System.Drawing.Font("Segoe UI", 9);
            _directionComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            _directionComboBox.Items.AddRange(new object[] {
                "↑ Вверх (по оси Z)",
                "↓ Вниз (по оси -Z)",
                "→ Вправо (по оси X)",
                "← Влево (по оси -X)",
                "↗ Вперед (по оси Y)",
                "↙ Назад (по оси -Y)"
            });
            _directionComboBox.SelectedIndex = 0;
            _directionComboBox.SelectedIndexChanged += (s, e) => UpdateDirection();

            distanceGroup.Controls.AddRange(new System.Windows.Forms.Control[] {
                _defaultDistanceRadio, _customDistanceRadio, _distanceNumeric,
                _unitsComboBox, distanceLabel, directionLabel, _directionComboBox
            });

            // Пояснение
            var explanationLabel = new System.Windows.Forms.Label();
            explanationLabel.Text = "Положительное значение - разместить выше, отрицательное - ниже исходного элемента\n" +
                                  "Выбор направления позволяет размещать элементы не только по вертикали, но и по горизонтали";
            explanationLabel.Font = new System.Drawing.Font("Segoe UI", 8);
            explanationLabel.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
            explanationLabel.Location = new System.Drawing.Point(20, 250);
            explanationLabel.Size = new System.Drawing.Size(500, 40);
            explanationLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Кнопка подтверждения
            var confirmButton = new System.Windows.Forms.Button();
            confirmButton.Text = "ПРИМЕНИТЬ РАССТОЯНИЕ";
            confirmButton.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            confirmButton.Location = new System.Drawing.Point(150, 300);
            confirmButton.Size = new System.Drawing.Size(200, 35);
            confirmButton.BackColor = System.Drawing.Color.FromArgb(40, 167, 69);
            confirmButton.ForeColor = System.Drawing.Color.White;
            confirmButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            confirmButton.DialogResult = System.Windows.Forms.DialogResult.OK;

            // Кнопка отмены
            var cancelButton = new System.Windows.Forms.Button();
            cancelButton.Text = "ОТМЕНА";
            cancelButton.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            cancelButton.Location = new System.Drawing.Point(370, 300);
            cancelButton.Size = new System.Drawing.Size(100, 35);
            cancelButton.BackColor = System.Drawing.Color.FromArgb(108, 117, 125);
            cancelButton.ForeColor = System.Drawing.Color.White;
            cancelButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;

            // Добавляем элементы
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                titleLabel, descLabel, distanceGroup, explanationLabel, confirmButton, cancelButton
            });

            // Назначаем кнопки
            this.AcceptButton = confirmButton;
            this.CancelButton = cancelButton;

            // Инициализируем состояние контролов
            UpdateControlsState();
            UpdateDirection();
        }

        private void UpdateControlsState()
        {
            bool customDistanceEnabled = _customDistanceRadio.Checked;
            _distanceNumeric.Enabled = customDistanceEnabled;
            _unitsComboBox.Enabled = customDistanceEnabled;
            _directionComboBox.Enabled = customDistanceEnabled;

            if (!customDistanceEnabled)
            {
                // Устанавливаем значение по умолчанию
                Distance = 0.5; // 0.5 метра по умолчанию
                _directionComboBox.SelectedIndex = 0; // Вверх
                UpdateDirection();
            }
        }

        private void UpdateDistanceUnits()
        {
            if (_unitsComboBox.SelectedItem?.ToString() == "миллиметры")
            {
                // Конвертируем метры в миллиметры
                _distanceNumeric.DecimalPlaces = 0;
                _distanceNumeric.Value = (decimal)(Distance * 1000);
                _distanceNumeric.Minimum = -100000;
                _distanceNumeric.Maximum = 100000;
                _distanceNumeric.Increment = 100;
            }
            else
            {
                // Конвертируем миллиметры в метры
                _distanceNumeric.DecimalPlaces = 2;
                _distanceNumeric.Value = (decimal)Distance;
                _distanceNumeric.Minimum = -100;
                _distanceNumeric.Maximum = 100;
                _distanceNumeric.Increment = 0.1M;
            }
        }

        private void UpdateDirection()
        {
            switch (_directionComboBox.SelectedIndex)
            {
                case 0: Direction = XYZ.BasisZ; break;       // Вверх
                case 1: Direction = -XYZ.BasisZ; break;      // Вниз
                case 2: Direction = XYZ.BasisX; break;       // Вправо
                case 3: Direction = -XYZ.BasisX; break;      // Влево
                case 4: Direction = XYZ.BasisY; break;       // Вперед
                case 5: Direction = -XYZ.BasisY; break;      // Назад
                default: Direction = XYZ.BasisZ; break;
            }
        }

        protected override void OnFormClosing(System.Windows.Forms.FormClosingEventArgs e)
        {
            if (this.DialogResult == System.Windows.Forms.DialogResult.OK)
            {
                if (_customDistanceRadio.Checked)
                {
                    // Сохраняем значение с учетом единиц измерения
                    if (_unitsComboBox.SelectedItem?.ToString() == "миллиметры")
                    {
                        Distance = (double)_distanceNumeric.Value / 1000.0; // Конвертируем мм в метры
                    }
                    else
                    {
                        Distance = (double)_distanceNumeric.Value;
                    }
                }
                else
                {
                    // Используем расстояние по умолчанию
                    Distance = 0.5;
                }

                // Обновляем направление
                UpdateDirection();
            }

            base.OnFormClosing(e);
        }
    }
}