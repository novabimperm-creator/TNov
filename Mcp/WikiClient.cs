using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using TNovCommon.Help;

namespace TNov.Mcp
{
    /// <summary>
    /// База знаний TNov для Claude: публичный API вики (тот же, что у tools\Build-HelpBundle.ps1).
    ///   GET /api/public/wiki/tree         — дерево страниц;
    ///   GET /api/public/wiki/page/{slug}  — статья (title, html).
    /// Без сети — вшитый бандл справки (HelpBundle).
    /// </summary>
    internal static class WikiClient
    {
        private const string BaseUrl = "https://tnov.pm-nova.ru";
        private const int MaxArticleChars = 30000;

        private sealed class Page
        {
            public string Slug;
            public string ParentSlug;
            public string Title;
        }

        private static readonly object _lock = new object();
        private static List<Page> _tree;
        private static DateTime _treeLoadedUtc;

        public static object Search(string query, int limit)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Пустой запрос.");
            limit = Math.Min(Math.Max(limit, 1), 50);

            List<Page> pages;
            string source = "tnov.pm-nova.ru";
            try { pages = Tree(); }
            catch (Exception)
            {
                pages = HelpBundle.Topics.Select(t => new Page { Slug = t.Slug, ParentSlug = t.ParentSlug, Title = t.Title }).ToList();
                source = "встроенная копия справки (сайт недоступен)";
            }
            var bySlug = pages.Where(p => p.Slug != null).GroupBy(p => p.Slug).ToDictionary(g => g.Key, g => g.First());

            // Сравнение по основам слов: русские окончания меняются, поэтому берём первые 5 букв.
            string[] stems = Regex.Split(query.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
                .Where(w => w.Length > 1)
                .Select(w => w.Length > 5 ? w.Substring(0, 5) : w)
                .Distinct()
                .ToArray();

            var results = pages
                .Select(p =>
                {
                    string parent = p.ParentSlug != null && bySlug.TryGetValue(p.ParentSlug, out Page pp) ? pp.Title : null;
                    string title = (p.Title ?? "").ToLowerInvariant();
                    string context = (parent ?? "").ToLowerInvariant();
                    int score = stems.Sum(s => title.Contains(s) ? 2 : context.Contains(s) ? 1 : 0);
                    return new { Page = p, Parent = parent, Score = score };
                })
                .Where(r => r.Score > 0)
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Page.Title)
                .Take(limit)
                .Select(r => new JObject
                {
                    ["slug"] = r.Page.Slug,
                    ["title"] = r.Page.Title,
                    ["section"] = r.Parent,
                    ["url"] = BaseUrl + "/wiki/" + r.Page.Slug
                });

            var found = new JArray(results);
            return new JObject
            {
                ["source"] = source,
                ["found"] = found.Count,
                ["articles"] = found,
                ["hint"] = found.Count == 0
                    ? "Ничего не найдено по заголовкам. Попробуйте другие слова или более общий термин."
                    : "Откройте подходящую статью через wiki_read(slug)."
            };
        }

        public static object Read(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Не указан slug.");

            string title, html, source = "tnov.pm-nova.ru";
            try
            {
                JObject page = JObject.Parse(Get("/api/public/wiki/page/" + Uri.EscapeDataString(slug)))["page"] as JObject
                    ?? throw new InvalidOperationException("Статья не найдена.");
                title = (string)page["title"];
                html = (string)page["html"];
            }
            catch (Exception ex)
            {
                if (!HelpBundle.TryGetTopicBySlug(slug, out HelpBundleTopic topic))
                    throw new InvalidOperationException($"Статья «{slug}» недоступна: {ex.Message}");
                title = topic.Title;
                html = HelpBundle.ReadArticle(topic);
                source = "встроенная копия справки (сайт недоступен)";
            }

            string text = HtmlToText(html ?? "");
            bool truncated = text.Length > MaxArticleChars;
            if (truncated) text = text.Substring(0, MaxArticleChars);
            return new JObject
            {
                ["title"] = title,
                ["url"] = BaseUrl + "/wiki/" + slug,
                ["source"] = source,
                ["text"] = text,
                ["truncated"] = truncated
            };
        }

        private static List<Page> Tree()
        {
            lock (_lock)
            {
                if (_tree != null && DateTime.UtcNow - _treeLoadedUtc < TimeSpan.FromHours(1)) return _tree;
            }
            JArray pages = JObject.Parse(Get("/api/public/wiki/tree"))["pages"] as JArray ?? new JArray();
            List<Page> tree = pages.OfType<JObject>()
                .Select(p => new Page { Slug = (string)p["slug"], ParentSlug = (string)p["parentSlug"], Title = (string)p["title"] })
                .ToList();
            lock (_lock)
            {
                _tree = tree;
                _treeLoadedUtc = DateTime.UtcNow;
            }
            return tree;
        }

        private static string Get(string path)
        {
            var request = (HttpWebRequest)WebRequest.Create(BaseUrl + path);
            request.Accept = "application/json";
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static string HtmlToText(string html)
        {
            string s = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<li[^>]*>", "\n- ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<h([1-6])[^>]*>", m => "\n\n" + new string('#', int.Parse(m.Groups[1].Value)) + " ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<(br|/p|/div|/h[1-6]|/tr|/li|/table)[^>]*>", "\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</t[dh]>", " | ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<img[^>]*alt=""([^""]*)""[^>]*>", "[рисунок: $1]", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<[^>]+>", "");
            s = WebUtility.HtmlDecode(s);
            s = Regex.Replace(s, @"[ \t]+", " ");
            s = Regex.Replace(s, @"\n\s*\n\s*\n+", "\n\n");
            return s.Trim();
        }
    }
}
