using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using System.Windows.Forms;
using System.Drawing;

namespace TNov
{
    public partial class LevelSelectionForm : System.Windows.Forms.Form
    {
        public Level SelectedLevel { get; private set; }
        private List<Level> _levels;
        private Document _linkDoc;
        private System.Windows.Forms.ListView _levelListView;

        public LevelSelectionForm(Document linkDoc)
        {
            _linkDoc = linkDoc;
            SelectedLevel = null;

            // Получаем все уровни из связанного файла
            _levels = new FilteredElementCollector(linkDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            InitializeForm();
            LoadLevels();
        }

        private void InitializeForm()
        {
            // Основные настройки формы
            this.Text = "Выбор этажа в связанном файле";
            this.Size = new System.Drawing.Size(600, 500);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(245, 255, 245);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Заголовок
            var titleLabel = new System.Windows.Forms.Label();
            titleLabel.Text = "ВЫБОР ЭТАЖА В СВЯЗАННОМ ФАЙЛЕ";
            titleLabel.Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
            titleLabel.ForeColor = System.Drawing.Color.FromArgb(0, 100, 0);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(550, 30);
            titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Описание
            var descLabel = new System.Windows.Forms.Label();
            descLabel.Text = "Выберите этаж, на котором будут размещаться элементы:";
            descLabel.Font = new System.Drawing.Font("Segoe UI", 10);
            descLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            descLabel.Location = new System.Drawing.Point(20, 60);
            descLabel.Size = new System.Drawing.Size(550, 25);
            descLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // ListView для уровней
            _levelListView = new System.Windows.Forms.ListView();
            _levelListView.Location = new System.Drawing.Point(20, 100);
            _levelListView.Size = new System.Drawing.Size(550, 300);
            _levelListView.View = System.Windows.Forms.View.Details;
            _levelListView.FullRowSelect = true;
            _levelListView.GridLines = true;
            _levelListView.Font = new System.Drawing.Font("Segoe UI", 9);
            _levelListView.BackColor = System.Drawing.Color.White;
            _levelListView.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            _levelListView.MultiSelect = false;

            // Колонки
            _levelListView.Columns.Add("Этаж", 200);
            _levelListView.Columns.Add("Отметка", 150);
            _levelListView.Columns.Add("ID", 150);

            // Панель предпросмотра
            var previewPanel = new System.Windows.Forms.Panel();
            previewPanel.Location = new System.Drawing.Point(20, 410);
            previewPanel.Size = new System.Drawing.Size(550, 25);
            previewPanel.BackColor = System.Drawing.Color.FromArgb(230, 255, 230);
            previewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

            var previewLabel = new System.Windows.Forms.Label();
            previewLabel.Text = "Выбрано: ничего";
            previewLabel.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            previewLabel.ForeColor = System.Drawing.Color.FromArgb(0, 100, 0);
            previewLabel.Location = new System.Drawing.Point(5, 3);
            previewLabel.Size = new System.Drawing.Size(540, 18);
            previewLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            previewLabel.Name = "previewLabel";
            previewPanel.Controls.Add(previewLabel);

            // Кнопка выбора
            var selectButton = new System.Windows.Forms.Button();
            selectButton.Text = "ВЫБРАТЬ ЭТАЖ";
            selectButton.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            selectButton.Location = new System.Drawing.Point(200, 450);
            selectButton.Size = new System.Drawing.Size(200, 35);
            selectButton.BackColor = System.Drawing.Color.FromArgb(40, 167, 69);
            selectButton.ForeColor = System.Drawing.Color.White;
            selectButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            selectButton.Click += (s, e) =>
            {
                if (SelectedLevel != null)
                {
                    this.DialogResult = System.Windows.Forms.DialogResult.OK;
                    this.Close();
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Пожалуйста, выберите этаж", "Внимание",
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                }
            };

            // Кнопка отмены
            var cancelButton = new System.Windows.Forms.Button();
            cancelButton.Text = "ОТМЕНА";
            cancelButton.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            cancelButton.Location = new System.Drawing.Point(420, 450);
            cancelButton.Size = new System.Drawing.Size(100, 35);
            cancelButton.BackColor = System.Drawing.Color.FromArgb(108, 117, 125);
            cancelButton.ForeColor = System.Drawing.Color.White;
            cancelButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;

            // Добавляем элементы
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                titleLabel, descLabel, _levelListView, previewPanel, selectButton, cancelButton
            });

            // События
            _levelListView.SelectedIndexChanged += (s, e) => UpdatePreview();
            _levelListView.DoubleClick += (s, e) =>
            {
                if (SelectedLevel != null)
                {
                    this.DialogResult = System.Windows.Forms.DialogResult.OK;
                    this.Close();
                }
            };

            // Назначаем кнопки
            this.AcceptButton = selectButton;
            this.CancelButton = cancelButton;
        }

        private void LoadLevels()
        {
            foreach (var level in _levels)
            {
                var item = new System.Windows.Forms.ListViewItem(new[] {
                    level.Name,
                    $"{level.Elevation:F3} м",
                    level.Id.ToString()
                });
                item.Tag = level;
                _levelListView.Items.Add(item);
            }

            // Выбираем первый элемент
            if (_levelListView.Items.Count > 0)
            {
                _levelListView.Items[0].Selected = true;
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            if (_levelListView.SelectedItems.Count > 0)
            {
                var selectedItem = _levelListView.SelectedItems[0];
                var levelName = selectedItem.SubItems[0].Text;
                var elevation = selectedItem.SubItems[1].Text;

                var previewLabel = this.Controls.Find("previewLabel", true).FirstOrDefault() as System.Windows.Forms.Label;
                if (previewLabel != null)
                {
                    previewLabel.Text = $"Выбрано: {levelName} (отметка: {elevation})";
                    SelectedLevel = selectedItem.Tag as Level;
                }
            }
            else
            {
                var previewLabel = this.Controls.Find("previewLabel", true).FirstOrDefault() as System.Windows.Forms.Label;
                if (previewLabel != null)
                {
                    previewLabel.Text = "Выбрано: ничего";
                    SelectedLevel = null;
                }
            }
        }
    }
}