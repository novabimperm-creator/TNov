<#
    HelpRegistry.ps1 — общие функции для работы со справкой TNov.

    Подключается через dot-source из Build-HelpBundle.ps1 и Check-Help.ps1:
        . (Join-Path $PSScriptRoot 'HelpRegistry.ps1')

    Реестр и порядок кнопок живут в C#, поэтому читаем их регулярками.
    Связка узкая и устойчивая: нас интересуют только литералы
    new HelpTopic("ключ", "слаг", ...) и GetHelpLink("ключ").
#>

Set-StrictMode -Version Latest

function Get-HelpLinksPath {
    param([string] $ToolsDir = $PSScriptRoot)
    Join-Path $ToolsDir '..\..\TNovCommon\HelpLinks.cs'
}

function Get-ApplicationPath {
    param([string] $ToolsDir = $PSScriptRoot)
    Join-Path $ToolsDir '..\Application.cs'
}

function Get-BundlePath {
    param([string] $ToolsDir = $PSScriptRoot)
    Join-Path $ToolsDir '..\..\TNovCommon\Help\help.tnpack'
}

<#
    Выбрасывает строки, закомментированные //. Разбор идёт регулярками по
    тексту, и без этого закомментированная запись реестра или кнопка ленты
    считались бы живыми — ровно та ошибка, из-за которой проверка молчала
    про ключ, оставшийся в коде после удаления из реестра.
#>
function Remove-CommentedLines {
    param([Parameter(Mandatory)][string] $Text)
    $kept = $Text -split "`n" | Where-Object { $_.TrimStart() -notmatch '^//' }
    return ($kept -join "`n")
}
function Read-TextFile {
    param([Parameter(Mandatory)][string] $Path)
    if (-not (Test-Path $Path)) { throw "нет файла: $Path" }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }
    return $text
}

<#
    Реестр HelpLinks: массив @{ Key; Slug; Title; DirectUrl; Order }.
    Order — позиция в файле, то есть порядок объявления.
#>
function Get-HelpRegistry {
    param([string] $Path = (Get-HelpLinksPath))

    $text = Remove-CommentedLines (Read-TextFile $Path)
    # Записи бывают многострочными и с необязательным directUrl.
    $rx = [regex]'new HelpTopic\(\s*"(?<key>[^"]*)"\s*,\s*(?:"(?<slug>[^"]*)"|null)\s*,\s*(?:\r?\n\s*)?"(?<title>[^"]*)"\s*(?:,\s*(?:\r?\n\s*)?(?:directUrl\s*:\s*)?@?"(?<url>[^"]*)"\s*)?\)'

    $result = New-Object System.Collections.ArrayList
    $i = 0
    foreach ($m in $rx.Matches($text)) {
        [void]$result.Add([pscustomobject]@{
            Key        = $m.Groups['key'].Value
            Slug       = if ($m.Groups['slug'].Success) { $m.Groups['slug'].Value } else { $null }
            DirectUrl  = if ($m.Groups['url'].Success) { $m.Groups['url'].Value } else { $null }
            Title      = $m.Groups['title'].Value
            Order      = $i
        })
        $i++
    }
    if ($result.Count -eq 0) { throw "не разобрал ни одного HelpTopic в $Path" }
    return $result
}

<#
    Порядок ключей так, как кнопки объявлены на ленте: последовательность
    GetHelpLink("...") в Application.cs. Дубликаты отбрасываем, оставляя
    первое вхождение — кнопка, объявленная раньше, задаёт позицию раздела.
#>
function Get-RibbonKeyOrder {
    param([string] $Path = (Get-ApplicationPath))

    $text = Remove-CommentedLines (Read-TextFile $Path)
    $order = New-Object System.Collections.Specialized.OrderedDictionary
    foreach ($m in ([regex]'GetHelpLink\(\s*"(?<key>[^"]*)"\s*\)').Matches($text)) {
        $key = $m.Groups['key'].Value
        if ($key -eq '-') { continue }
        if (-not $order.Contains($key)) { $order.Add($key, $order.Count) }
    }
    return $order
}

<#
    Слаг -> позиция на ленте. Если на слаг указывает несколько ключей,
    берём самый ранний: раздел встаёт туда, где первая ссылающаяся кнопка.
#>
function Get-SlugRibbonOrder {
    param(
        [Parameter(Mandatory)] $Registry,
        [Parameter(Mandatory)] $RibbonOrder
    )
    $map = @{}
    foreach ($topic in $Registry) {
        if ([string]::IsNullOrEmpty($topic.Slug)) { continue }
        if (-not $RibbonOrder.Contains($topic.Key)) { continue }
        $pos = [int]$RibbonOrder[$topic.Key]
        if (-not $map.ContainsKey($topic.Slug) -or $pos -lt $map[$topic.Slug]) {
            $map[$topic.Slug] = $pos
        }
    }
    return $map
}

<# Манифест бандла. $null, если бандла нет. #>
function Get-HelpBundleManifest {
    param([string] $Path = (Get-BundlePath))

    if (-not (Test-Path $Path)) { return $null }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $Path))
    try {
        $entry = $zip.GetEntry('manifest.json')
        if (-not $entry) { return $null }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { return $reader.ReadToEnd() | ConvertFrom-Json }
        finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }
}

<# Ключи, которые реально передаются в GetHelpLink/ShowHelp по всему решению. #>
function Get-UsedHelpKeys {
    param([string] $SolutionRoot = (Join-Path $PSScriptRoot '..\..'))

    $keys = New-Object System.Collections.Generic.HashSet[string]
    $files = Get-ChildItem -Path $SolutionRoot -Recurse -Filter *.cs -File -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -notmatch '\\(bin|obj|packages|\.vs|\.git|ignore)\\' }
    foreach ($file in $files) {
        $text = Remove-CommentedLines ([System.IO.File]::ReadAllText($file.FullName))
        if ($text -notmatch 'GetHelpLink|ShowHelp') { continue }
        foreach ($m in ([regex]'(?:GetHelpLink|ShowHelp)\(\s*"(?<key>[^"]*)"\s*\)').Matches($text)) {
            [void]$keys.Add($m.Groups['key'].Value)
        }
    }
    return $keys
}
