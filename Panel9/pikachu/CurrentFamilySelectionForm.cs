using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using System.Windows.Forms;
using System.Drawing;

namespace TNov
{
    public partial class CurrentFamilySelectionForm : System.Windows.Forms.Form
    {
        public Autodesk.Revit.DB.Family SelectedFamily { get; private set; }
        private List<Autodesk.Revit.DB.Family> _filteredFamilies;
        private Autodesk.Revit.DB.Document _doc;
        private System.Windows.Forms.ListView _familyListView;
        private System.Windows.Forms.TextBox _searchTextBox;
        private System.Windows.Forms.Label _statsLabel;

        // Категории для семейства Б (электрооборудование)
        private static readonly BuiltInCategory[] _allowedCategoriesB = new BuiltInCategory[]
        {
            BuiltInCategory.OST_ElectricalEquipment,    // Электрооборудование
            BuiltInCategory.OST_CommunicationDevices,   // Устройства связи
            BuiltInCategory.OST_NurseCallDevices,       // Устройства вызова и оповещения
            BuiltInCategory.OST_FireAlarmDevices,       // Пожарная сигнализация
            BuiltInCategory.OST_SecurityDevices         // Датчики безопасности
        };

        public CurrentFamilySelectionForm(Autodesk.Revit.DB.Document doc)
        {
            _doc = doc;
            SelectedFamily = null;

            // Фильтруем семейства только по разрешенным категориям
            _filteredFamilies = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Family))
                .Cast<Autodesk.Revit.DB.Family>()
                .Where(f => f.FamilyCategory != null &&
                           IsCategoryAllowed(f.FamilyCategory.Id))
                .OrderBy(f => f.Name)
                .ToList();

            InitializeForm();
            LoadFamilies();
        }

        private bool IsCategoryAllowed(ElementId categoryId)
        {
            foreach (var allowedCategory in _allowedCategoriesB)
            {
                if (new ElementId(allowedCategory) == categoryId)
                    return true;
            }
            return false;
        }

        private void InitializeForm()
        {
            // Основные настройки формы
            this.Text = "Выбор семейства из текущего файла";
            this.Size = new System.Drawing.Size(1000, 700);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(255, 248, 225);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.MaximizeBox = true;

            // Заголовок
            var titleLabel = new System.Windows.Forms.Label();
            titleLabel.Text = "ВЫБОР СЕМЕЙСТВА Б (ЭЛЕКТРООБОРУДОВАНИЕ)";
            titleLabel.Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
            titleLabel.ForeColor = System.Drawing.Color.FromArgb(193, 115, 0);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(950, 30);
            titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Описание
            var descLabel = new System.Windows.Forms.Label();
            descLabel.Text = "Выберите семейство электрооборудования для размещения рядом с элементами ОВ/ВК:";
            descLabel.Font = new System.Drawing.Font("Segoe UI", 10);
            descLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            descLabel.Location = new System.Drawing.Point(20, 60);
            descLabel.Size = new System.Drawing.Size(950, 25);
            descLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Панель поиска
            var searchPanel = new System.Windows.Forms.Panel();
            searchPanel.Location = new System.Drawing.Point(20, 95);
            searchPanel.Size = new System.Drawing.Size(950, 35);
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
            _searchTextBox.TextChanged += (s, e) => FilterFamilies();

            var searchButton = new System.Windows.Forms.Button();
            searchButton.Text = "Найти";
            searchButton.Font = new System.Drawing.Font("Segoe UI", 8, System.Drawing.FontStyle.Bold);
            searchButton.Location = new System.Drawing.Point(370, 5);
            searchButton.Size = new System.Drawing.Size(60, 25);
            searchButton.BackColor = System.Drawing.Color.FromArgb(40, 167, 69);
            searchButton.ForeColor = System.Drawing.Color.White;
            searchButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            searchButton.Click += (s, e) => FilterFamilies();

            var clearSearchButton = new System.Windows.Forms.Button();
            clearSearchButton.Text = "Очистить";
            clearSearchButton.Font = new System.Drawing.Font("Segoe UI", 8, System.Drawing.FontStyle.Bold);
            clearSearchButton.Location = new System.Drawing.Point(435, 5);
            clearSearchButton.Size = new System.Drawing.Size(70, 25);
            clearSearchButton.BackColor = System.Drawing.Color.FromArgb(108, 117, 125);
            clearSearchButton.ForeColor = System.Drawing.Color.White;
            clearSearchButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            clearSearchButton.Click += (s, e) =>
            {
                _searchTextBox.Text = "";
                FilterFamilies();
            };

            // Статистика
            _statsLabel = new System.Windows.Forms.Label();
            _statsLabel.Text = $"Семейств электрооборудования: {_filteredFamilies.Count}";
            _statsLabel.Font = new System.Drawing.Font("Segoe UI", 9);
            _statsLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            _statsLabel.Location = new System.Drawing.Point(520, 8);
            _statsLabel.Size = new System.Drawing.Size(420, 20);
            _statsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

            searchPanel.Controls.AddRange(new System.Windows.Forms.Control[] {
                searchLabel, _searchTextBox, searchButton, clearSearchButton, _statsLabel
            });

            // ListView для семейств
            _familyListView = new System.Windows.Forms.ListView();
            _familyListView.Location = new System.Drawing.Point(20, 145);
            _familyListView.Size = new System.Drawing.Size(950, 450);
            _familyListView.View = System.Windows.Forms.View.Details;
            _familyListView.FullRowSelect = true;
            _familyListView.GridLines = true;
            _familyListView.Font = new System.Drawing.Font("Segoe UI", 9);
            _familyListView.BackColor = System.Drawing.Color.White;
            _familyListView.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            _familyListView.MultiSelect = false;

            // Колонки
            _familyListView.Columns.Add("Семейство", 500);
            _familyListView.Columns.Add("Категория", 350);
            _familyListView.Columns.Add("Типов", 100);

            // Сортировка по колонкам
            _familyListView.ColumnClick += (s, e) => SortListView(e.Column);

            // Панель предпросмотра
            var previewPanel = new System.Windows.Forms.Panel();
            previewPanel.Location = new System.Drawing.Point(20, 605);
            previewPanel.Size = new System.Drawing.Size(950, 25);
            previewPanel.BackColor = System.Drawing.Color.FromArgb(255, 243, 205);
            previewPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

            var previewLabel = new System.Windows.Forms.Label();
            previewLabel.Text = "Выбрано: ничего";
            previewLabel.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            previewLabel.ForeColor = System.Drawing.Color.FromArgb(133, 100, 4);
            previewLabel.Location = new System.Drawing.Point(5, 3);
            previewLabel.Size = new System.Drawing.Size(940, 18);
            previewLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            previewLabel.Name = "previewLabel";
            previewPanel.Controls.Add(previewLabel);

            // Кнопка выбора
            var selectButton = new System.Windows.Forms.Button();
            selectButton.Text = "ВЫБРАТЬ СЕМЕЙСТВО Б";
            selectButton.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            selectButton.Location = new System.Drawing.Point(300, 640);
            selectButton.Size = new System.Drawing.Size(250, 35);
            selectButton.BackColor = System.Drawing.Color.FromArgb(40, 167, 69);
            selectButton.ForeColor = System.Drawing.Color.White;
            selectButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            selectButton.Click += (s, e) =>
            {
                if (SelectedFamily != null)
                {
                    this.DialogResult = System.Windows.Forms.DialogResult.OK;
                    this.Close();
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Пожалуйста, выберите семейство", "Внимание",
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                }
            };

            // Кнопка отмены
            var cancelButton = new System.Windows.Forms.Button();
            cancelButton.Text = "ОТМЕНА";
            cancelButton.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
            cancelButton.Location = new System.Drawing.Point(570, 640);
            cancelButton.Size = new System.Drawing.Size(100, 35);
            cancelButton.BackColor = System.Drawing.Color.FromArgb(108, 117, 125);
            cancelButton.ForeColor = System.Drawing.Color.White;
            cancelButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;

            // Добавляем элементы
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                titleLabel, descLabel, searchPanel, _familyListView, previewPanel, selectButton, cancelButton
            });

            // События
            _familyListView.SelectedIndexChanged += (s, e) => UpdatePreview();
            _familyListView.DoubleClick += (s, e) =>
            {
                if (SelectedFamily != null)
                {
                    this.DialogResult = System.Windows.Forms.DialogResult.OK;
                    this.Close();
                }
            };
            _searchTextBox.KeyDown += (s, e) => { if (e.KeyCode == System.Windows.Forms.Keys.Enter) FilterFamilies(); };

            // Назначаем кнопки
            this.AcceptButton = selectButton;
            this.CancelButton = cancelButton;
        }

        private void LoadFamilies()
        {
            foreach (var family in _filteredFamilies)
            {
                var symbolCount = family.GetFamilySymbolIds().Count;

                var item = new System.Windows.Forms.ListViewItem(new[] {
                    family.Name,
                    family.FamilyCategory?.Name ?? "Неизвестно",
                    symbolCount.ToString()
                });
                item.Tag = family;

                // Подсветка МДУ семейств
                string familyName = family.Name.ToUpper();
                if (familyName.Contains("МДУ") || familyName.Contains("MDU"))
                {
                    item.ForeColor = System.Drawing.Color.FromArgb(0, 100, 0);
                    item.Font = new System.Drawing.Font(_familyListView.Font, System.Drawing.FontStyle.Bold);
                }

                _familyListView.Items.Add(item);
            }

            UpdateStats();

            // Выбираем первый элемент
            if (_familyListView.Items.Count > 0)
            {
                _familyListView.Items[0].Selected = true;
                UpdatePreview();
            }
        }

        private void FilterFamilies()
        {
            string searchText = _searchTextBox.Text.Trim().ToLower();

            _familyListView.Items.Clear();
            _familyListView.BeginUpdate();

            var filtered = _filteredFamilies.Where(family =>
                family.Name.ToLower().Contains(searchText) ||
                (family.FamilyCategory?.Name ?? "").ToLower().Contains(searchText));

            foreach (var family in filtered)
            {
                var symbolCount = family.GetFamilySymbolIds().Count;

                var item = new System.Windows.Forms.ListViewItem(new[] {
                    family.Name,
                    family.FamilyCategory?.Name ?? "Неизвестно",
                    symbolCount.ToString()
                });
                item.Tag = family;

                string familyName = family.Name.ToUpper();
                if (familyName.Contains("МДУ") || familyName.Contains("MDU"))
                {
                    item.ForeColor = System.Drawing.Color.FromArgb(0, 100, 0);
                    item.Font = new System.Drawing.Font(_familyListView.Font, System.Drawing.FontStyle.Bold);
                }

                _familyListView.Items.Add(item);
            }

            _familyListView.EndUpdate();
            UpdateStats();
            UpdatePreview();
        }

        private void UpdateStats()
        {
            string searchText = _searchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                _statsLabel.Text = $"Семейств электрооборудования: {_familyListView.Items.Count}";
            }
            else
            {
                _statsLabel.Text = $"Найдено: {_familyListView.Items.Count}";
            }
        }

        private void SortListView(int columnIndex)
        {
            if (_familyListView.Items.Count == 0) return;

            _familyListView.ListViewItemSorter = new ListViewItemComparer(columnIndex);
            _familyListView.Sort();
        }

        private void UpdatePreview()
        {
            if (_familyListView.SelectedItems.Count > 0)
            {
                var selectedItem = _familyListView.SelectedItems[0];
                var familyName = selectedItem.SubItems[0].Text;
                var category = selectedItem.SubItems[1].Text;

                var previewLabel = this.Controls.Find("previewLabel", true).FirstOrDefault() as System.Windows.Forms.Label;
                if (previewLabel != null)
                {
                    previewLabel.Text = $"Выбрано: {familyName} ({category})";
                    SelectedFamily = selectedItem.Tag as Autodesk.Revit.DB.Family;
                }
            }
            else
            {
                var previewLabel = this.Controls.Find("previewLabel", true).FirstOrDefault() as System.Windows.Forms.Label;
                if (previewLabel != null)
                {
                    previewLabel.Text = "Выбрано: ничего";
                    SelectedFamily = null;
                }
            }
        }

        // Класс для сортировки ListView
        private class ListViewItemComparer : System.Collections.IComparer
        {
            private int _columnIndex;

            public ListViewItemComparer(int columnIndex)
            {
                _columnIndex = columnIndex;
            }

            public int Compare(object x, object y)
            {
                System.Windows.Forms.ListViewItem itemX = (System.Windows.Forms.ListViewItem)x;
                System.Windows.Forms.ListViewItem itemY = (System.Windows.Forms.ListViewItem)y;

                string textX = itemX.SubItems[_columnIndex].Text;
                string textY = itemY.SubItems[_columnIndex].Text;

                // Для колонки "Типов" сортируем как числа
                if (_columnIndex == 2 && int.TryParse(textX, out int numX) && int.TryParse(textY, out int numY))
                {
                    return numX.CompareTo(numY);
                }

                // Остальные колонки сортируем как строки
                return string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}