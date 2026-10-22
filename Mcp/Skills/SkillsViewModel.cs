using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace TNov.Mcp.Skills
{
    /// <summary>Файл скилла в карточке.</summary>
    public sealed class SkillFileItem
    {
        internal SkillFileItem(SkillFile file, bool canEdit) { File = file; CanEdit = canEdit; }
        internal SkillFile File { get; }
        public string Name => File.Name;
        public string Meta => SkillFiles.SizeText(File.Size) + (string.IsNullOrEmpty(File.AddedBy) ? "" : " · " + File.AddedBy);
        public bool CanEdit { get; }
    }

    /// <summary>Скилл в списке вкладки «Скиллы».</summary>
    public sealed class SkillItem
    {
        internal SkillItem(Skill skill)
        {
            Skill = skill;
            CanEdit = SkillLibrary.CanEdit(skill);
            Files = (skill.Files ?? new List<SkillFile>()).Select(f => new SkillFileItem(f, CanEdit)).ToList();
        }

        internal Skill Skill { get; }
        public string Name => Skill.Name;
        public string Author => "Автор: " + Skill.Author;
        public string Instruction => string.IsNullOrWhiteSpace(Skill.Instruction) ? "—" : Skill.Instruction;
        public List<SkillFileItem> Files { get; }
        public bool HasFiles => Files.Count > 0;
        /// <summary>Автор скилла или отдел BIM: правит инструкцию и файлы.</summary>
        public bool CanEdit { get; }
        public bool ReadOnlyInstruction => !CanEdit;
        public string Description => string.IsNullOrWhiteSpace(Skill.Description) ? "—" : Skill.Description;
        public string StatusText => Skill.StatusText;
        public bool IsPublished => Skill.Status == Skill.Published;
        public string Meta
        {
            get
            {
                SkillVersion v = Skill.Latest;
                string revit = string.IsNullOrEmpty(v?.Revit) ? "" : $" · Revit {v.Revit}";
                return $"v{v?.Number}{revit} · {Skill.UpdatedUtc.ToLocalTime():dd.MM.yyyy}";
            }
        }
        /// <summary>Строка в списке: автор всегда на виду.</summary>
        public string ListMeta => Skill.Author + " · " + Meta;
        public string ParametersText => Skill.Parameters == null || Skill.Parameters.Count == 0
            ? "нет"
            : string.Join("\n", Skill.Parameters.Select(p => $"• {p.Name}" + (string.IsNullOrEmpty(p.Description) ? "" : " — " + p.Description) +
                                                             (string.IsNullOrEmpty(p.Default) ? "" : $" (по умолчанию: {p.Default})")));
        public string ExamplesText => Skill.Examples == null || Skill.Examples.Count == 0
            ? "—" : string.Join("\n", Skill.Examples.Select(e => "• " + e));
        public string Code => Skill.Latest?.Code;
        public bool CanPublish => SkillLibrary.CanPublish(Skill);
        public bool CanHide => SkillLibrary.CanHide(Skill);
        public bool CanRestore => SkillLibrary.CanRestore(Skill);
    }

    /// <summary>
    /// Вкладка «Скиллы» окна «Нейросетошная»: список, поиск, публикация черновиков, скрытие.
    /// Обращения к хранилищу (шара или TNovApi) — в фоне, окно не подвисает.
    /// </summary>
    public sealed class SkillsViewModel : INotifyPropertyChanged
    {
        private string _query = "";
        private SkillItem _selected;
        private string _message;
        private bool _busy;
        private bool _loaded;

        // Поток окна: в Revit у него может не быть контекста синхронизации, и await вернётся на фоновый поток.
        private readonly System.Windows.Threading.Dispatcher _ui = System.Windows.Threading.Dispatcher.CurrentDispatcher;

        private void OnUi(Action action)
        {
            if (_ui.CheckAccess()) action();
            else _ui.Invoke(action);
        }

        public ObservableCollection<SkillItem> Items { get; } = new ObservableCollection<SkillItem>();

        public bool Available => SkillLibrary.Available;
        public string UnavailableText => "Библиотека скиллов доступна только в корпоративной конфигурации.";

        public string Query
        {
            get => _query;
            set { if (_query == value) return; _query = value ?? ""; OnPropertyChanged(); Load(false); }
        }

        public SkillItem Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                _instructionDraft = value?.Skill.Instruction ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(InstructionDraft));
            }
        }

        private string _instructionDraft = "";

        /// <summary>Текст инструкции в поле правки (у автора и отдела BIM).</summary>
        public string InstructionDraft
        {
            get => _instructionDraft;
            set { _instructionDraft = value ?? ""; OnPropertyChanged(); }
        }

        public void SaveInstruction()
        {
            SkillItem item = _selected;
            if (item == null) return;
            string text = _instructionDraft;
            RunEdit(() => SkillLibrary.SetInstruction(item.Skill.Id, text), $"Инструкция скилла «{item.Name}» сохранена.");
        }

        public void AddFiles(string[] paths)
        {
            SkillItem item = _selected;
            if (item == null || paths == null || paths.Length == 0) return;
            RunEdit(() => { foreach (string p in paths) SkillFiles.Add(item.Skill.Id, p); },
                paths.Length == 1 ? $"Файл «{System.IO.Path.GetFileName(paths[0])}» прикреплён." : $"Прикреплено файлов: {paths.Length}.");
        }

        public void RemoveFile(string name)
        {
            SkillItem item = _selected;
            if (item == null) return;
            RunEdit(() => SkillFiles.Remove(item.Skill.Id, name), $"Файл «{name}» удалён.");
        }

        public void ReplaceFile(string name, string path)
        {
            SkillItem item = _selected;
            if (item == null) return;
            RunEdit(() => SkillFiles.Replace(item.Skill.Id, name, path), $"Файл «{name}» заменён.");
        }

        public async void OpenFile(string name)
        {
            SkillItem item = _selected;
            SkillFile file = item?.Skill.Files?.FirstOrDefault(f => f.Name == name);
            if (file == null) return;
            try { await Task.Run(() => SkillFiles.Open(item.Skill.Id, file)); }
            catch (Exception ex) { OnUi(() => Message = "Не удалось открыть файл: " + ex.Message); }
        }

        /// <summary>Правка в фоне, затем перезагрузка списка с сохранением выбора.</summary>
        private async void RunEdit(Action edit, string done)
        {
            Busy = true;
            try
            {
                await Task.Run(edit);
                OnUi(() => { Message = done; Load(true); });
            }
            catch (Exception ex)
            {
                OnUi(() => { Message = "Не удалось: " + ex.Message; Busy = false; });
            }
        }

        public bool HasSelection => _selected != null;

        public string Message
        {
            get => _message;
            private set { _message = value; OnPropertyChanged(); }
        }

        public bool Busy
        {
            get => _busy;
            private set { _busy = value; OnPropertyChanged(); }
        }

        /// <summary>Первое открытие вкладки — загрузить список.</summary>
        public void EnsureLoaded()
        {
            if (_loaded || !Available) return;
            _loaded = true;
            Load(true);
        }

        public async void Load(bool force)
        {
            if (!Available) return;
            string query = _query;
            string selectedId = _selected?.Skill.Id;
            Busy = true;
            try
            {
                List<Skill> skills = await Task.Run(() => string.IsNullOrWhiteSpace(query)
                    ? SkillLibrary.All(force)
                    : SkillLibrary.Search(query, 200));
                string store = await Task.Run(() => SkillLibrary.StoreName);
                OnUi(() =>
                {
                    if (query != _query) return; // пока грузили, запрос уже поменялся
                    Items.Clear();
                    foreach (Skill skill in skills
                                 .OrderBy(s => s.Status == Skill.Published ? 0 : s.Status == Skill.Draft ? 1 : 2)
                                 .ThenBy(s => string.IsNullOrWhiteSpace(query) ? s.Name : ""))
                        Items.Add(new SkillItem(skill));
                    Selected = Items.FirstOrDefault(i => i.Skill.Id == selectedId);
                    Message = Items.Count == 0
                        ? (string.IsNullOrWhiteSpace(query)
                            ? "Скиллов пока нет. Попросите нейросеть сохранить удачное решение: «сохрани это как скилл»."
                            : "Ничего не найдено.")
                        : $"Скиллов: {Items.Count} · хранилище: {store}";
                });
            }
            catch (Exception ex)
            {
                OnUi(() => Message = "Не удалось загрузить скиллы: " + ex.Message);
            }
            finally
            {
                OnUi(() => Busy = false);
            }
        }

        public void Publish() => ChangeStatus(Skill.Published, "опубликован");
        public void Restore() => ChangeStatus(Skill.Published, "снова виден всем");
        public void Hide() => ChangeStatus(Skill.Hidden, "скрыт");

        private async void ChangeStatus(string status, string done)
        {
            SkillItem item = _selected;
            if (item == null) return;
            Busy = true;
            try
            {
                await Task.Run(() => SkillLibrary.SetStatus(item.Skill.Id, status));
                OnUi(() =>
                {
                    Message = $"Скилл «{item.Name}» {done}.";
                    Load(true);
                });
            }
            catch (Exception ex)
            {
                OnUi(() =>
                {
                    Message = "Не удалось: " + ex.Message;
                    Busy = false;
                });
            }
        }

        public void CopyCode()
        {
            if (_selected?.Code == null) return;
            try { Clipboard.SetText(_selected.Code); Message = "Код скопирован."; }
            catch (Exception) { Message = "Не удалось скопировать — выделите код и нажмите Ctrl+C."; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
