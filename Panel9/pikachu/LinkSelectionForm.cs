using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using System.Windows.Forms;
using System.Drawing;

namespace TNov
{
    public partial class LinkSelectionForm : System.Windows.Forms.Form
    {
        public RevitLinkInstance SelectedLink { get; private set; }
        private List<RevitLinkInstance> _linkInstances;
        private System.Windows.Forms.ListView _linkListView;
        private System.Windows.Forms.TextBox _searchTextBox;

        public LinkSelectionForm(Document doc)
        {
            SelectedLink = null;

            // Получаем все связанные файлы
            _linkInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(l => l.GetLinkDocument() != null)
                .OrderBy(l => l.Name)
                .ToList();

            InitializeForm();
            LoadLinks();
        }

        private void InitializeForm()
        {
            // Основные настройки формы
            this.Text = "Выбор связанного файла";
            this.Size = new System.Drawing.Size(800, 500);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(245, 245, 255);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Заголовок
            var titleLabel = new System.Windows.Forms.Label();
            titleLabel.Text = "ВЫБОР СВЯЗАННОГО ФАЙЛА";
            titleLabel.Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
            titleLabel.ForeColor = System.Drawing.Color.FromArgb(0, 80, 160);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(750, 30);
            titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Описание
            var descLabel = new System.Windows.Forms.Label();
            descLabel.Text = "Выберите связанный файл для работы:";
            descLabel.Font = new System.Drawing.Font("Segoe UI", 10);
            descLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            descLabel.Location = new System.Drawing.Point(20, 60);
            descLabel.Size = new System.Drawing.Size(750, 25);
            descLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Панель поиска
            var searchPanel = new System.Windows.Forms.Panel();
            searchPanel.Location = new System.Drawing.Point(20, 95);
            searchPanel.Size = new System.Drawing.Size(750, 35);
            searchPanel.BackColor = System.Drawing.Color.White;
            searchPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

            var searchLabel = new System.Windows.Forms.Label();
            searchLabel.Text = "Поиск:";
            searchLabel.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            searchLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            searchLabel.Location = new System.Drawing.Point(10, 8);
            searchLabel.Size = new System.Drawing.Size(50, 20);
            searchLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            _searchTextBox = new System.Windows.Forms.TextBox();
            _searchTextBox.Location = new System.Drawing.Point(65, 6);
            _searchTextBox.Size = new System.Drawing.Size(300, 23);
            _searchTextBox.Font = new System.Drawing.Font("Segoe UI", 9);
            _searchTextBox.TextChanged += (s, e) => FilterLinks();

            // Статистика
            var statsLabel = new System.Windows.Forms.Label();
            statsLabel.Text = $"Найдено связанных файлов: {_linkInstances.Count}";
            statsLabel.Font = new System.Drawing.Font("Segoe UI", 9);
            statsLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            statsLabel.Location = new System.Drawing.Point(400, 8);
            statsLabel.Size = new System.Drawing.Size(340, 20);
            statsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

            searchPanel.Controls.AddRange(new System.Windows.Forms.Control[] {
                searchLabel, _searchTextBox, statsLabel
            });

            // ListView для связанных файлов
            _linkListView = new System.Windows.Forms.ListView();
            _linkListView.Location = new System.Drawing.Point(20, 145);
            _linkListView.Size = new System.Drawing.Size(750, 250);
            _linkListView.View = System.Windows.Forms.View.Details;
            _linkListView.FullRowSelect = true;
            _linkListView.GridLines = true;
            _linkListView.Font = new System.Drawing.Font("Segoe UI", 9);
            _linkListView.BackColor = System.Drawing.Color.White;
            _linkListView.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            _linkListView.MultiSelect = false;

            // Колонки
            _linkListView.Columns.Add("Имя файла", 400);
            _linkListView.Columns.Add("Тип", 200);
            _linkListView.Columns.Add("Статус", 150);

            // Кнопка выбора
            var selectButton = new System.Windows.Forms.Button();
            selectButton.Text = "ВЫБРАТЬ ФАЙЛ";
            selectButton.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            selectButton.Location = new System.Drawing.Point(300, 410);
            selectButton.Size = new System.Drawing.Size(200, 35);
            selectButton.BackColor = System.Drawing.Color.FromArgb(0, 123, 255);
            selectButton.ForeColor = System.Drawing.Color.White;
            selectButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            selectButton.Click += (s, e) =>
            {
                if (SelectedLink != null)
                {
                    this.DialogResult = System.Windows.Forms.DialogResult.OK;
                    this.Close();
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Пожалуйста, выберите связанный файл", "Внимание",
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                }
            };

            // Кнопка отмены
            var cancelButton = new System.Windows.Forms.Button();
            cancelButton.Text = "ОТМЕНА";
            cancelButton.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            cancelButton.Location = new System.Drawing.Point(520, 410);
            cancelButton.Size = new System.Drawing.Size(100, 35);
            cancelButton.BackColor = System.Drawing.Color.FromArgb(108, 117, 125);
            cancelButton.ForeColor = System.Drawing.Color.White;
            cancelButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;

            // Добавляем элементы
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                titleLabel, descLabel, searchPanel, _linkListView, selectButton, cancelButton
            });

            // События
            _linkListView.SelectedIndexChanged += (s, e) => UpdateSelection();
            _linkListView.DoubleClick += (s, e) =>
            {
                if (SelectedLink != null)
                {
                    this.DialogResult = System.Windows.Forms.DialogResult.OK;
                    this.Close();
                }
            };
            _searchTextBox.KeyDown += (s, e) => { if (e.KeyCode == System.Windows.Forms.Keys.Enter) FilterLinks(); };

            // Назначаем кнопки
            this.AcceptButton = selectButton;
            this.CancelButton = cancelButton;
        }

        private void LoadLinks()
        {
            foreach (var link in _linkInstances)
            {
                var linkDoc = link.GetLinkDocument();
                var item = new System.Windows.Forms.ListViewItem(new[] {
                    linkDoc?.Title ?? "Неизвестно",
                    link.GetType().Name,
                    "Доступен"
                });
                item.Tag = link;
                _linkListView.Items.Add(item);
            }

            // Выбираем первый элемент
            if (_linkListView.Items.Count > 0)
            {
                _linkListView.Items[0].Selected = true;
                UpdateSelection();
            }
        }

        private void FilterLinks()
        {
            string searchText = _searchTextBox.Text.Trim().ToLower();

            _linkListView.Items.Clear();
            _linkListView.BeginUpdate();

            var filtered = _linkInstances.Where(link =>
            {
                var doc = link.GetLinkDocument();
                return doc?.Title.ToLower().Contains(searchText) == true;
            });

            foreach (var link in filtered)
            {
                var linkDoc = link.GetLinkDocument();
                var item = new System.Windows.Forms.ListViewItem(new[] {
                    linkDoc?.Title ?? "Неизвестно",
                    link.GetType().Name,
                    "Доступен"
                });
                item.Tag = link;
                _linkListView.Items.Add(item);
            }

            _linkListView.EndUpdate();
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            if (_linkListView.SelectedItems.Count > 0)
            {
                SelectedLink = _linkListView.SelectedItems[0].Tag as RevitLinkInstance;
            }
            else
            {
                SelectedLink = null;
            }
        }
    }
}
