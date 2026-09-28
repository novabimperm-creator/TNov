using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using SchemeBuilder.Commands;

namespace TNov
{
    /// <summary>
    /// Значки ленты с учётом темы Revit. Светлые — из Properties.Resources (ключ resx),
    /// тёмные — вшитые resources\dark\{ключ}.png (генерирует tools\MakeDarkIcons.ps1).
    /// Нет тёмного варианта — берётся светлый. Тема есть только в R2027; в R2022 всегда светлая.
    /// </summary>
    internal static class RibbonIcons
    {
        /// <summary>Префикс ключей значков SchemeBuilder: они вшиты в TNovSS, а не в TNov.</summary>
        public const string SchemeBuilderPrefix = "SchemeBuilder.";

        private const string DarkResourcePrefix = "TNov.Dark.";

        private static readonly Dictionary<string, BitmapSource> _cache = new Dictionary<string, BitmapSource>();

        // Имя кнопки -> (малый, большой) ключ значка; по нему значки переназначаются при смене темы.
        private static readonly Dictionary<string, (string Small, string Large)> _buttons =
            new Dictionary<string, (string, string)>(StringComparer.Ordinal);

        public static bool IsDark
        {
            get
            {
#if R2027
                try { return UIThemeManager.CurrentTheme == UITheme.Dark; }
                catch (Exception) { return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>Значок по ключу resx (или SchemeBuilderPrefix + имя файла без .png) для текущей темы.</summary>
        public static BitmapSource Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            bool dark = IsDark;
            string cacheKey = (dark ? "d:" : "l:") + name;
            if (_cache.TryGetValue(cacheKey, out BitmapSource cached)) return cached;

            BitmapSource result = null;
            if (dark)
            {
                using (Stream stream = typeof(RibbonIcons).Assembly.GetManifestResourceStream(DarkResourcePrefix + name + ".png"))
                {
                    if (stream != null) result = FromStream(stream);
                }
            }
            if (result == null) result = LoadLight(name);

            if (result != null && result.CanFreeze) result.Freeze();
            _cache[cacheKey] = result;
            return result;
        }

        /// <summary>Назначает значки кнопке и запоминает их, чтобы переключать при смене темы.</summary>
        public static void Set(ButtonData data, string small, string large = null)
        {
            if (small != null) data.Image = Get(small);
            if (large != null) data.LargeImage = Get(large);
            _buttons[data.Name] = (small, large);
        }

        /// <summary>Переназначает значки всем запомненным кнопкам вкладки под текущую тему.</summary>
        public static void Apply(UIControlledApplication application, string tabName)
        {
            foreach (RibbonPanel panel in application.GetRibbonPanels(tabName))
            {
                foreach (RibbonItem item in panel.GetItems())
                {
                    ApplyTo(item);
                    if (item is PulldownButton pulldown) // SplitButton — наследник PulldownButton
                    {
                        foreach (PushButton child in pulldown.GetItems())
                            ApplyTo(child);
                    }
                }
            }
        }

        private static void ApplyTo(RibbonItem item)
        {
            if (!(item is RibbonButton button)) return;
            if (!_buttons.TryGetValue(button.Name, out var icons)) return;

            try
            {
                if (icons.Small != null) button.Image = Get(icons.Small);
                if (icons.Large != null) button.LargeImage = Get(icons.Large);
            }
            catch (Exception) { }
        }

        private static BitmapSource LoadLight(string name)
        {
            if (name.StartsWith(SchemeBuilderPrefix, StringComparison.Ordinal))
            {
                string resourceName = "SchemeBuilder.Resources." + name.Substring(SchemeBuilderPrefix.Length) + ".png";
                using (Stream stream = typeof(OpenWizardCommand).Assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null) return FromStream(stream);
                }
                return FromImage(Properties.Resources.ssNumberer16);
            }

            return Properties.Resources.ResourceManager.GetObject(name, Properties.Resources.Culture) is System.Drawing.Image img
                ? FromImage(img)
                : null;
        }

        private static BitmapSource FromImage(System.Drawing.Image img)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                img.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                return FromStream(ms);
            }
        }

        private static BitmapSource FromStream(Stream stream)
        {
            BitmapImage bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = stream;
            bmp.EndInit();
            return bmp;
        }
    }
}
