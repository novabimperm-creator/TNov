using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TNov
{
    /// <summary>
    /// Класс для хранения настроек плагина
    /// </summary>
    public class PluginSettings
    {
        public string LastVisibilityMode { get; set; } = "Все кнопки";
        public DateTime LastSaveDate { get; set; } = DateTime.Now;
        public string UserName { get; set; } = Environment.UserName;

        // Можно добавить другие настройки
        public bool ShowTooltips { get; set; } = true;
        public bool AutoSaveSettings { get; set; } = true;
        public List<string> FavoriteModes { get; set; } = new List<string>();
    }

    /// <summary>
    /// Менеджер для работы с настройками плагина
    /// </summary>
    public class SettingsManager
    {
        private static readonly string SettingsFilePath;
        private static PluginSettings _currentSettings;

        static SettingsManager()
        {
            // Определяем путь для сохранения настроек
            SettingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovVisibilitySettings.json");
        }

        /// <summary>
        /// Загружает настройки из файла
        /// </summary>
        public static PluginSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    _currentSettings = JsonConvert.DeserializeObject<PluginSettings>(json); 
                    return _currentSettings;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
            }

            // Если файла нет или произошла ошибка, возвращаем настройки по умолчанию
            return new PluginSettings();
        }

        /// <summary>
        /// Сохраняет текущие настройки в файл
        /// </summary>
        public static void SaveSettings(PluginSettings settings = null)
        {
            try
            {
                
                // Обновляем дату сохранения
                if (settings == null)
                {
                    if (_currentSettings == null)
                        _currentSettings = new PluginSettings();
                    _currentSettings.LastSaveDate = DateTime.Now;
                }
                else
                {
                    settings.LastSaveDate = DateTime.Now;
                    _currentSettings = settings;
                }

                File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(_currentSettings));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
            }
        }

        /// <summary>
        /// Возвращает текущие настройки
        /// </summary>
        public static PluginSettings CurrentSettings
        {
            get
            {
                if (_currentSettings == null)
                    _currentSettings = LoadSettings();
                return _currentSettings;
            }
        }

        /// <summary>
        /// Обновляет конкретную настройку
        /// </summary>
        public static void UpdateSetting<T>(string propertyName, T value)
        {
            if (_currentSettings == null)
                _currentSettings = LoadSettings();

            var property = typeof(PluginSettings).GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(_currentSettings, value);
                SaveSettings();
            }
        }
    }
}