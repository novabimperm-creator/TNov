using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TNovApi.Client;
using TNovCommon;
using TNovCommon.Server;
using TNovCommon.Storage;

namespace TNov.Mcp.Skills
{
    /// <summary>
    /// Файлы скиллов (шаблоны, примеры, схемы — то, что пригодится коллегам вместе с рецептом).
    ///   шара:  {ServerPath}mcp\skills\files\{id скилла}\{имя};
    ///   API:   /api/files (kind mcp-skill-file, scope = id скилла), локальная копия для открытия —
    ///          %LOCALAPPDATA%\TNov\cache\mcp-skills\{id}\{имя}.
    /// Список файлов хранится в самом скилле (Skill.Files). Методы — вне UI-потока.
    /// </summary>
    internal static class SkillFiles
    {
        public const string ApiKind = "mcp-skill-file";
        public const long MaxSize = 50L * 1024 * 1024;

        private static TNovApiClient Api(out string shareRoot)
        {
            TNovConfig config = TNovConfigLoad.GetCachedConfig();
            shareRoot = Path.Combine(ServerData.NormalizeRoot(config.ServerPath), "mcp", "skills", "files");
            return string.IsNullOrWhiteSpace(config.ApiUrl) ? null : DocumentStores.GetClient(config);
        }

        /// <summary>Добавить файл к скиллу (файл с тем же именем заменяется). Права проверяет вызывающий.</summary>
        public static Skill Add(string skillId, string localPath, bool requireEdit = true)
        {
            var info = new FileInfo(localPath);
            if (!info.Exists) throw new FileNotFoundException("Файл не найден.", localPath);
            if (info.Length > MaxSize) throw new InvalidOperationException($"Файл «{info.Name}» больше {MaxSize / 1024 / 1024} МБ.");
            string name = info.Name;
            if (!SafePaths.IsSafeFileName(name)) throw new InvalidOperationException($"Недопустимое имя файла «{name}».");

            Skill current = SkillLibrary.Get(skillId) ?? throw new InvalidOperationException("Скилл не найден.");
            if (requireEdit && !SkillLibrary.CanEdit(current))
                throw new InvalidOperationException("Прикреплять файлы могут автор скилла и сотрудники отдела BIM.");
            SkillFile old = current.Files?.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

            TNovApiClient api = Api(out string shareRoot);
            string fileId = null;
            if (api != null)
            {
                using (FileStream fs = File.OpenRead(localPath))
                {
                    TNovFileInfo uploaded = Run(() => api.UploadFileAsync(ApiKind, skillId, "", name, fs, "application/octet-stream"));
                    fileId = uploaded.Id.ToString();
                }
            }
            else
            {
                string folder = Path.Combine(shareRoot, skillId);
                Directory.CreateDirectory(folder);
                File.Copy(localPath, Path.Combine(folder, name), true);
            }

            Skill skill = SkillLibrary.ModifyFiles(skillId, files =>
            {
                files.RemoveAll(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
                files.Add(new SkillFile { Name = name, FileId = fileId, Size = info.Length, AddedBy = Environment.UserName, AddedUtc = DateTime.UtcNow });
            }, requireEdit);

            // Старая версия того же файла в API — отдельный объект, убираем.
            if (api != null && old?.FileId != null && Guid.TryParse(old.FileId, out Guid oldId))
                TryRun(() => api.DeleteFileAsync(oldId));
            return skill;
        }

        /// <summary>Убрать файл из скилла и из хранилища.</summary>
        public static Skill Remove(string skillId, string name)
        {
            Skill current = SkillLibrary.Get(skillId) ?? throw new InvalidOperationException("Скилл не найден.");
            if (!SkillLibrary.CanEdit(current))
                throw new InvalidOperationException("Удалять файлы могут автор скилла и сотрудники отдела BIM.");
            SkillFile file = current.Files?.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (file == null) return current;

            Skill skill = SkillLibrary.ModifyFiles(skillId,
                files => files.RemoveAll(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)), requireEdit: true);

            TNovApiClient api = Api(out string shareRoot);
            if (file.FileId != null)
            {
                if (api != null && Guid.TryParse(file.FileId, out Guid id)) TryRun(() => api.DeleteFileAsync(id));
            }
            else if (SafePaths.IsSafeFileName(file.Name))
            {
                try { File.Delete(Path.Combine(shareRoot, skillId, file.Name)); } catch (Exception) { }
            }
            return skill;
        }

        /// <summary>Заменить файл другим (новое имя или то же).</summary>
        public static Skill Replace(string skillId, string oldName, string localPath)
        {
            Skill skill = Add(skillId, localPath);
            if (!string.Equals(oldName, Path.GetFileName(localPath), StringComparison.OrdinalIgnoreCase))
                skill = Remove(skillId, oldName);
            return skill;
        }

        /// <summary>Открыть файл программой по умолчанию (из API — через локальную копию).</summary>
        public static void Open(string skillId, SkillFile file)
        {
            if (!SafePaths.IsSafeFileName(file.Name)) throw new InvalidOperationException("Недопустимое имя файла.");
            TNovApiClient api = Api(out string shareRoot);
            string path;
            if (file.FileId != null)
            {
                if (api == null || !Guid.TryParse(file.FileId, out Guid id)) throw new InvalidOperationException("Файл хранится в TNovApi, а он недоступен.");
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TNov", "cache", "mcp-skills", skillId);
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, file.Name);
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                    if (!Run(() => api.DownloadFileAsync(id, fs))) throw new FileNotFoundException("Файл не найден на сервере.");
            }
            else
            {
                path = Path.Combine(shareRoot, skillId, file.Name);
                if (!File.Exists(path)) throw new FileNotFoundException("Файл не найден на шаре.", path);
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        public static string SizeText(long size) =>
            size >= 1024 * 1024 ? $"{size / 1024.0 / 1024.0:0.#} МБ" : size >= 1024 ? $"{size / 1024.0:0} КБ" : $"{size} Б";

        private static T Run<T>(Func<Task<T>> action) => Task.Run(action).GetAwaiter().GetResult();

        private static void TryRun(Func<Task<bool>> action)
        {
            try { Run(action); } catch (Exception) { } // не удалось удалить старый объект — не критично
        }
    }
}
