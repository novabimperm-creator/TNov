<#
    Build-HelpBundle.ps1 — собирает бандл справки для панели TNov.

    Источник — вики плагина (единственный источник правды по контенту):
        GET /api/public/wiki/tree          дерево страниц
        GET /api/public/wiki/page/{slug}   статья: title, html, deckSrc
        GET /uploads/wiki/{file}           картинки

    Берётся поддерево RootSlug (узел «TNov») — страницы функций плагина.
    Страницы-колоды (задан deckSrc) пропускаются: pptx в справке не используем.

    Результат — help.tnpack (zip):
        manifest.json      список разделов
        html/<slug>.xhtml  нормализованный XHTML-фрагмент
        img/<файл>         картинки

    Скрипт требует сети, поэтому НЕ вызывается из сборки — запускается вручную
    или в релизном прогоне, а help.tnpack коммитится в репозиторий.

    Примеры:
        pwsh -File tools\Build-HelpBundle.ps1 -WhatIfReport
        pwsh -File tools\Build-HelpBundle.ps1
#>
[CmdletBinding()]
param(
    [string]   $BaseUrl        = 'https://tnov.pm-nova.ru',
    [string]   $RootSlug       = 'tnovporyadokustanovki',
    [string[]] $ExtraSlugs     = @(),
    [string]   $OutFile        = (Join-Path $PSScriptRoot '..\..\TNovCommon\Help\help.tnpack'),
    [int]      $MaxImageWidth  = 600,
    [int]      $MaxBundleBytes = 1572864,
    [int]      $JpegQuality    = 85,
    # Во сколько раз jpeg должен быть меньше png, чтобы выбрать его.
    # Порог, а не простой минимум: на скриншотах с мелким текстом jpeg мылит,
    # поэтому выигрыш в пару процентов не стоит потери качества.
    [double]   $JpegAdvantage  = 1.5,
    [switch]   $WhatIfReport
)

$ErrorActionPreference = 'Stop'

# Разбор реестра и порядка кнопок — общий с Check-Help.ps1.
. (Join-Path $PSScriptRoot 'HelpRegistry.ps1')

# System.Drawing нужен только для ужатия картинок. Если его нет (PS без
# Windows Desktop-сборок) — кладём картинки как есть, это не повод падать.
$canResize = $false
try {
    Add-Type -AssemblyName System.Drawing -ErrorAction Stop
    $canResize = $true
}
catch {
    Write-Host 'System.Drawing недоступен — картинки не ужимаются.' -ForegroundColor DarkYellow
}

# --------------------------------------------------------------------------
# Нормализатор HTML -> XHTML.
#
# Панель читает XHTML через XmlReader с DtdProcessing.Prohibit, поэтому на
# выходе не должно остаться ни именованных сущностей (&nbsp; и подобные —
# это жёсткий XmlException), ни незакрытых тегов, ни DOCTYPE.
# --------------------------------------------------------------------------
$normalizerSource = @'
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

public static class WikiHtmlNormalizer
{
    // Теги, которые переживают нормализацию. Остальные разворачиваются:
    // сам тег выбрасывается, текст внутри сохраняется.
    private static readonly HashSet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "p","div","span","h1","h2","h3","h4","ul","ol","li","br","b","strong","i","em","u",
        "code","pre","a","img","table","thead","tbody","tr","th","td","blockquote","hr",
        "details","summary","section"
    };

    private static readonly HashSet<string> Void = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "br","img","hr"
    };

    private static readonly Dictionary<string, string[]> KeptAttributes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "img", new[] { "src", "alt" } },
        { "a",   new[] { "href" } }
    };

    private static readonly Regex TagRegex = new Regex(
        @"<\s*(?<close>/)?\s*(?<name>[A-Za-z][A-Za-z0-9]*)(?<attrs>[^>]*?)(?<self>/)?\s*>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AttrRegex = new Regex(
        @"(?<name>[A-Za-z_:][-A-Za-z0-9_:.]*)\s*=\s*(?:""(?<v1>[^""]*)""|'(?<v2>[^']*)'|(?<v3>[^\s""'>]+))",
        RegexOptions.Compiled);

    private static readonly Regex ImgSrcRegex = new Regex(
        @"<\s*img\b[^>]*?\bsrc\s*=\s*(?:""(?<v>[^""]*)""|'(?<w>[^']*)')",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// html из API в XHTML-фрагмент, обёрнутый в article.
    /// imageMap: исходный src (/uploads/wiki/x.png) -> путь в бандле (img/x.png).
    /// </summary>
    public static string Normalize(string html, Dictionary<string, string> imageMap)
    {
        if (html == null) html = string.Empty;

        var output = new StringBuilder();
        var openTags = new Stack<string>();
        int pos = 0;

        output.Append("<article>");

        foreach (Match tag in TagRegex.Matches(html))
        {
            AppendText(output, html.Substring(pos, tag.Index - pos));
            pos = tag.Index + tag.Length;

            string name = tag.Groups["name"].Value.ToLowerInvariant();
            if (!Allowed.Contains(name))
                continue;

            bool isClose = tag.Groups["close"].Success;

            if (Void.Contains(name))
            {
                if (isClose) continue;
                output.Append('<').Append(name);
                AppendAttributes(output, name, tag.Groups["attrs"].Value, imageMap);
                output.Append(" />");
                continue;
            }

            if (isClose)
            {
                // Закрываем только реально открытое, иначе XHTML развалится.
                if (!openTags.Contains(name)) continue;
                while (openTags.Count > 0)
                {
                    string top = openTags.Pop();
                    output.Append("</").Append(top).Append('>');
                    if (string.Equals(top, name, StringComparison.OrdinalIgnoreCase)) break;
                }
                continue;
            }

            output.Append('<').Append(name);
            AppendAttributes(output, name, tag.Groups["attrs"].Value, imageMap);

            if (tag.Groups["self"].Success)
            {
                output.Append(" />");
                continue;
            }

            output.Append('>');
            openTags.Push(name);
        }

        AppendText(output, html.Substring(pos));

        while (openTags.Count > 0)
            output.Append("</").Append(openTags.Pop()).Append('>');

        output.Append("</article>");
        return output.ToString();
    }

    private static void AppendAttributes(StringBuilder output, string tagName, string attrs, Dictionary<string, string> imageMap)
    {
        string[] kept;
        if (!KeptAttributes.TryGetValue(tagName, out kept)) return;

        foreach (Match a in AttrRegex.Matches(attrs ?? string.Empty))
        {
            string name = a.Groups["name"].Value.ToLowerInvariant();
            if (Array.IndexOf(kept, name) < 0) continue;

            string value = a.Groups["v1"].Success ? a.Groups["v1"].Value
                         : a.Groups["v2"].Success ? a.Groups["v2"].Value
                         : a.Groups["v3"].Value;

            value = WebUtility.HtmlDecode(value ?? string.Empty);

            if (string.Equals(tagName, "img", StringComparison.OrdinalIgnoreCase)
                && string.Equals(name, "src", StringComparison.OrdinalIgnoreCase))
            {
                string mapped;
                if (imageMap != null && imageMap.TryGetValue(value, out mapped))
                    value = mapped;
            }

            output.Append(' ').Append(name).Append("=\"").Append(EscapeXml(value)).Append('"');
        }
    }

    private static void AppendText(StringBuilder output, string raw)
    {
        if (string.IsNullOrEmpty(raw)) return;
        output.Append(EscapeXml(WebUtility.HtmlDecode(raw)));
    }

    private static string EscapeXml(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '&':  sb.Append("&amp;");  break;
                case '<':  sb.Append("&lt;");   break;
                case '>':  sb.Append("&gt;");   break;
                case '"':  sb.Append("&quot;"); break;
                case '\'': sb.Append("&apos;"); break;
                default:
                    // Недопустимые в XML 1.0 символы выбрасываем: иначе упадёт XmlReader.
                    if (c == '\t' || c == '\n' || c == '\r'
                        || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD))
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Все src из тегов img.</summary>
    public static List<string> FindImageSources(string html)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(html)) return result;

        foreach (Match m in ImgSrcRegex.Matches(html))
        {
            string src = m.Groups["v"].Success ? m.Groups["v"].Value : m.Groups["w"].Value;
            src = WebUtility.HtmlDecode(src ?? string.Empty);
            if (src.Length > 0 && !result.Contains(src))
                result.Add(src);
        }
        return result;
    }
}
'@

if (-not ('WikiHtmlNormalizer' -as [type])) {
    Add-Type -TypeDefinition $normalizerSource
}

# --------------------------------------------------------------------------
# Вспомогательные функции
# --------------------------------------------------------------------------
function Get-WikiJson {
    param([Parameter(Mandatory)][string] $Path)
    Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + $Path) -Headers @{ Accept = 'application/json' } -TimeoutSec 60
}

function Get-SubtreePages {
    param(
        [Parameter(Mandatory)] $Pages,
        [Parameter(Mandatory)][string] $Root
    )
    $byParent = @{}
    foreach ($p in $Pages) {
        $key = if ($null -eq $p.parentSlug) { '' } else { [string]$p.parentSlug }
        if (-not $byParent.ContainsKey($key)) { $byParent[$key] = New-Object System.Collections.ArrayList }
        [void]$byParent[$key].Add($p)
    }

    $result = New-Object System.Collections.ArrayList
    $queue = New-Object System.Collections.Generic.Queue[string]
    $queue.Enqueue($Root)
    while ($queue.Count -gt 0) {
        $slug = $queue.Dequeue()
        if (-not $byParent.ContainsKey($slug)) { continue }
        foreach ($child in ($byParent[$slug] | Sort-Object sort, title)) {
            [void]$result.Add($child)
            $queue.Enqueue([string]$child.slug)
        }
    }
    return $result
}

function Get-RemoteBytes {
    param([Parameter(Mandatory)][string] $Url)
    $tmp = [System.IO.Path]::GetTempFileName()
    try {
        Invoke-WebRequest -Uri $Url -OutFile $tmp -TimeoutSec 60 | Out-Null
        return [System.IO.File]::ReadAllBytes($tmp)
    }
    finally { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
}

function Save-ImageBytes {
    param(
        [Parameter(Mandatory)][System.Drawing.Image] $Image,
        [Parameter(Mandatory)][string] $Format,
        [int] $Quality = 85
    )
    $stream = New-Object System.IO.MemoryStream
    try {
        if ($Format -eq 'jpeg') {
            $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
                       Where-Object { $_.MimeType -eq 'image/jpeg' }
            $params = New-Object System.Drawing.Imaging.EncoderParameters 1
            $params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter (
                [System.Drawing.Imaging.Encoder]::Quality), $Quality
            $Image.Save($stream, $encoder, $params)
        }
        else {
            $Image.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        return $stream.ToArray()
    }
    finally { $stream.Dispose() }
}

<#
    Готовит картинку под панель: сужает до MaxWidth и выбирает формат.

    Скриншоты интерфейса лучше жмутся в png, фотографии и градиенты — в jpeg,
    поэтому кодируем в оба и берём png, если jpeg не выигрывает хотя бы в
    JpegAdvantage раз. Исходник побеждает оба варианта, когда он уже меньше:
    перекодирование мелкой иконки только раздувает её.

    Возвращает @{ Bytes; Ext } — расширение может отличаться от исходного.
#>
function Get-OptimizedImage {
    param(
        [Parameter(Mandatory)][byte[]] $Bytes,
        [Parameter(Mandatory)][string] $SourceName,
        [Parameter(Mandatory)][int]    $MaxWidth
    )
    $sourceExt = [System.IO.Path]::GetExtension($SourceName)
    if (-not $canResize) { return @{ Bytes = $Bytes; Ext = $sourceExt } }

    $inStream = New-Object System.IO.MemoryStream(, $Bytes)
    $image = $null
    $resized = $null
    try {
        try { $image = [System.Drawing.Image]::FromStream($inStream) }
        catch { return @{ Bytes = $Bytes; Ext = $sourceExt } }   # формат не распознан

        $work = $image
        if ($image.Width -gt $MaxWidth) {
            $height = [Math]::Max(1, [int][Math]::Round($image.Height * ($MaxWidth / $image.Width)))
            $resized = New-Object System.Drawing.Bitmap $MaxWidth, $height
            $g = [System.Drawing.Graphics]::FromImage($resized)
            try {
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $g.DrawImage($image, 0, 0, $MaxWidth, $height)
            }
            finally { $g.Dispose() }
            $work = $resized
        }

        $png  = Save-ImageBytes -Image $work -Format 'png'
        $jpeg = Save-ImageBytes -Image $work -Format 'jpeg' -Quality $JpegQuality

        if ($jpeg.Length * $JpegAdvantage -lt $png.Length) {
            $best = @{ Bytes = $jpeg; Ext = '.jpg' }
        }
        else {
            $best = @{ Bytes = $png; Ext = '.png' }
        }

        # Не сужали и исходник не хуже — оставляем его: без потерь и без роста.
        if ($null -eq $resized -and $Bytes.Length -le $best.Bytes.Length) {
            return @{ Bytes = $Bytes; Ext = $sourceExt }
        }

        return $best
    }
    finally {
        if ($resized) { $resized.Dispose() }
        if ($image) { $image.Dispose() }
        $inStream.Dispose()
    }
}

function Assert-ReadableXhtml {
    param(
        [Parameter(Mandatory)][string] $Xhtml,
        [Parameter(Mandatory)][string] $Slug
    )
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.IgnoreWhitespace = $false

    $reader = New-Object System.IO.StringReader $Xhtml
    $xml = [System.Xml.XmlReader]::Create($reader, $settings)
    try { while ($xml.Read()) { } }
    catch { throw ("XHTML страницы '{0}' не читается XmlReader: {1}" -f $Slug, $_.Exception.Message) }
    finally { $xml.Dispose(); $reader.Dispose() }
}

# --------------------------------------------------------------------------
# Сбор
# --------------------------------------------------------------------------
Write-Host "Источник: $BaseUrl" -ForegroundColor Cyan

$tree  = Get-WikiJson '/api/public/wiki/tree'
$pages = Get-SubtreePages -Pages $tree.pages -Root $RootSlug

$slugs = New-Object System.Collections.ArrayList
foreach ($p in $pages) { [void]$slugs.Add([string]$p.slug) }
foreach ($s in $ExtraSlugs) { if ($slugs -notcontains $s) { [void]$slugs.Add($s) } }

Write-Host ("Страниц в поддереве '{0}': {1}" -f $RootSlug, $slugs.Count)

# Порядок разделов в панели должен совпадать с порядком кнопок на ленте.
$ribbonBySlug = Get-SlugRibbonOrder -Registry (Get-HelpRegistry) -RibbonOrder (Get-RibbonKeyOrder)
Write-Host ("Порядок разделов берётся из ленты: {0} слагов сопоставлено кнопкам" -f $ribbonBySlug.Count)

$topics  = New-Object System.Collections.ArrayList
$images  = @{}
$skipped = New-Object System.Collections.ArrayList

foreach ($slug in $slugs) {
    $page = (Get-WikiJson ('/api/public/wiki/page/' + [uri]::EscapeDataString($slug))).page

    if ($page.deckSrc) {
        [void]$skipped.Add([pscustomobject]@{ Slug = $slug; Title = $page.title })
        Write-Host ("  пропуск  {0,-22} {1} (pptx-колода)" -f $slug, $page.title) -ForegroundColor DarkYellow
        continue
    }

    $imageMap = New-Object 'System.Collections.Generic.Dictionary[string,string]'
    foreach ($src in [WikiHtmlNormalizer]::FindImageSources($page.html)) {
        if ($src -notlike '/uploads/*') { continue }

        if (-not $images.ContainsKey($src)) {
            $name = [System.IO.Path]::GetFileName($src.Split('?')[0])
            $raw  = Get-RemoteBytes ($BaseUrl.TrimEnd('/') + $src)
            $opt  = Get-OptimizedImage -Bytes $raw -SourceName $name -MaxWidth $MaxImageWidth

            # Формат мог смениться, поэтому имя в бандле строим заново.
            $entryName = [System.IO.Path]::GetFileNameWithoutExtension($name) + $opt.Ext
            $images[$src] = @{ Entry = 'img/' + $entryName; Bytes = $opt.Bytes; Source = $raw.Length }
        }

        $imageMap[$src] = $images[$src].Entry
    }

    $xhtml = [WikiHtmlNormalizer]::Normalize($page.html, $imageMap)
    Assert-ReadableXhtml -Xhtml $xhtml -Slug $slug

    # sort берём из Application.cs, а не из поля sort вики: оглавление панели
    # должно идти в том же порядке, в каком объявлены кнопки. Разделы, на
    # которые не ведёт ни одна кнопка, уходят в конец, сохраняя порядок вики.
    if ($ribbonBySlug.ContainsKey([string]$page.slug)) {
        $sort = [int]$ribbonBySlug[[string]$page.slug]
    }
    else {
        $sort = 10000
        $match = $pages | Where-Object { $_.slug -eq $page.slug } | Select-Object -First 1
        if ($match) { $sort += [int]$match.sort }
    }

    [void]$topics.Add([pscustomobject]@{
        slug       = [string]$page.slug
        title      = [string]$page.title
        parentSlug = [string]$page.parentSlug
        sort       = $sort
        file       = 'html/' + $page.slug + '.xhtml'
        updatedAt  = $(if ($page.updatedAt -is [datetime]) { $page.updatedAt.ToUniversalTime().ToString('o') } else { [string]$page.updatedAt })
        xhtml      = $xhtml
    })

    $stamp = if ($page.updatedAt -is [datetime]) { $page.updatedAt.ToLocalTime().ToString('dd.MM HH:mm') } else { [string]$page.updatedAt }
    Write-Host ("  ok       {0,-22} {1,-32} правлена {2}" -f $page.slug, $page.title, $stamp) -ForegroundColor Green
}

$manifest = [pscustomobject]@{
    generated = (Get-Date).ToUniversalTime().ToString('o')
    source    = $BaseUrl
    rootSlug  = $RootSlug
    topics    = @($topics | ForEach-Object {
        [pscustomobject]@{
            slug = $_.slug; title = $_.title; parentSlug = $_.parentSlug
            sort = $_.sort; file = $_.file; updatedAt = $_.updatedAt
        }
    })
}

if ($WhatIfReport) {
    Write-Host ''
    Write-Host ("Разделов {0}, картинок {1}, пропущено колод {2}" -f $topics.Count, $images.Count, $skipped.Count)
    $manifest.topics | Format-Table slug, title, updatedAt -AutoSize
    return
}

# --------------------------------------------------------------------------
# Запись бандла
# --------------------------------------------------------------------------
$outDir = Split-Path -Parent $OutFile
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }

$utf8 = New-Object System.Text.UTF8Encoding $false
$memory = New-Object System.IO.MemoryStream
$zip = New-Object System.IO.Compression.ZipArchive($memory, [System.IO.Compression.ZipArchiveMode]::Create, $true)
try {
    foreach ($item in @(
        @{ Name = 'manifest.json'; Bytes = $utf8.GetBytes(($manifest | ConvertTo-Json -Depth 6)) }
    ) + @($topics | ForEach-Object { @{ Name = $_.file; Bytes = $utf8.GetBytes($_.xhtml) } }
    ) + @($images.Keys | ForEach-Object { @{ Name = $images[$_].Entry; Bytes = $images[$_].Bytes } })) {

        $entry = $zip.CreateEntry($item.Name, [System.IO.Compression.CompressionLevel]::Optimal)
        $stream = $entry.Open()
        try { $stream.Write($item.Bytes, 0, $item.Bytes.Length) } finally { $stream.Dispose() }
    }
}
finally { $zip.Dispose() }

$bundle = $memory.ToArray()
$memory.Dispose()

if ($bundle.Length -gt $MaxBundleBytes) {
    throw ("Бандл {0:N0} байт — больше лимита {1:N0}. Уберите тяжёлые страницы или уменьшите MaxImageWidth." -f $bundle.Length, $MaxBundleBytes)
}

[System.IO.File]::WriteAllBytes($OutFile, $bundle)

Write-Host ''
Write-Host ("Готово: {0}" -f (Resolve-Path $OutFile)) -ForegroundColor Cyan
Write-Host ("  разделов {0}, картинок {1}, размер {2:N0} байт (лимит {3:N0})" -f $topics.Count, $images.Count, $bundle.Length, $MaxBundleBytes)
$srcBytes = ($images.Keys | ForEach-Object { $images[$_].Source } | Measure-Object -Sum).Sum
$optBytes = ($images.Keys | ForEach-Object { $images[$_].Bytes.Length } | Measure-Object -Sum).Sum
if ($srcBytes -gt 0) {
    Write-Host ("  картинки: {0:N0} -> {1:N0} байт ({2:P0} от исходного)" -f $srcBytes, $optBytes, ($optBytes / $srcBytes))
}
$share = $bundle.Length / $MaxBundleBytes
if ($share -gt 0.75) {
    Write-Host ("  ВНИМАНИЕ: занято {0:P0} лимита — пора уменьшать MaxImageWidth" -f $share) -ForegroundColor Yellow
    Write-Host "  или выносить тяжёлые статьи из узла TNov." -ForegroundColor Yellow
}
if ($skipped.Count -gt 0) {
    Write-Host ("  пропущено колод: {0}" -f $skipped.Count) -ForegroundColor DarkYellow
    $skipped | ForEach-Object { Write-Host ("    {0} — {1}" -f $_.Slug, $_.Title) -ForegroundColor DarkYellow }
}
