<#
    Check-Help.ps1 — сверяет три списка, которые легко разъезжаются молча:

        реестр HelpLinks.cs  <->  бандл help.tnpack  <->  вики плагина

    Зачем: бандл собирается отдельным скриптом и не версионируется, поэтому
    «прописал слаг, но не перегенерировал» и «завёл статью, но не привязал
    ключ» не видны ни в сборке, ни в интерфейсе.

    Код возврата: 0 — расхождений нет, 1 — есть (годится для CI).

    Примеры:
        pwsh -File tools\Check-Help.ps1
        pwsh -File tools\Check-Help.ps1 -NoNetwork     # без обращений к вики
#>
[CmdletBinding()]
param(
    [string] $BaseUrl  = 'https://tnov.pm-nova.ru',
    [string] $RootSlug = 'tnovporyadokustanovki',
    [switch] $NoNetwork
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'HelpRegistry.ps1')

$problems = 0
$notes    = 0

function Write-Problem { param([string] $Text) ; $script:problems++ ; Write-Host "  ✗ $Text" -ForegroundColor Red }
function Write-Note    { param([string] $Text) ; $script:notes++    ; Write-Host "  · $Text" -ForegroundColor DarkYellow }
function Write-Ok      { param([string] $Text) ; Write-Host "  ok $Text" -ForegroundColor Green }
function Write-Section { param([string] $Text) ; Write-Host '' ; Write-Host $Text -ForegroundColor Cyan }

# ---------------------------------------------------------------------------
$registry = Get-HelpRegistry
$manifest = Get-HelpBundleManifest
$ribbon   = Get-RibbonKeyOrder

Write-Host ("Реестр: {0} ключей. Бандл: {1}. Кнопок на ленте со справкой: {2}." -f
            $registry.Count,
            $(if ($manifest) { "$($manifest.topics.Count) статей, собран $($manifest.generated)" } else { 'НЕТ' }),
            $ribbon.Count)

# --- 1. ключи, которые код запрашивает, а реестр не знает --------------------
Write-Section '1. Ключи из кода отсутствуют в реестре'
$used = Get-UsedHelpKeys
$known = @{}
foreach ($t in $registry) { $known[$t.Key] = $true }
$unknown = @($used | Where-Object { -not $known.ContainsKey($_) })
if ($unknown.Count -eq 0) { Write-Ok 'все ключи из кода есть в реестре' }
else { foreach ($k in ($unknown | Sort-Object)) { Write-Problem "код просит «$k» — в реестре нет, уйдёт в корень вики" } }

# --- 2. мёртвые ключи реестра ------------------------------------------------
Write-Section '2. Ключи реестра, которых нет в коде'
$dead = @($registry | Where-Object { $_.Key -ne '-' -and -not $used.Contains($_.Key) })
if ($dead.Count -eq 0) { Write-Ok 'неиспользуемых ключей нет' }
else { foreach ($t in $dead) { Write-Note ("«{0}» не используется нигде в коде" -f $t.Key) } }

# --- 3. статьи бандла без ключа ----------------------------------------------
Write-Section '3. Статьи в бандле, не привязанные к ключу'
if (-not $manifest) { Write-Problem 'бандла нет — запустите tools\Build-HelpBundle.ps1' }
else {
    $slugToKeys = @{}
    foreach ($t in $registry) {
        if ([string]::IsNullOrEmpty($t.Slug)) { continue }
        if (-not $slugToKeys.ContainsKey($t.Slug)) { $slugToKeys[$t.Slug] = @() }
        $slugToKeys[$t.Slug] += $t.Key
    }
    $orphans = @($manifest.topics | Where-Object { -not $slugToKeys.ContainsKey($_.slug) })
    if ($orphans.Count -eq 0) { Write-Ok 'все статьи бандла привязаны' }
    else {
        foreach ($o in $orphans) {
            Write-Problem ("«{0}» ({1}) есть в панели, но ни одна кнопка на неё не ведёт" -f $o.title, $o.slug)
        }
    }
}

# --- 4. ключи со слагом, которого нет в бандле --------------------------------
Write-Section '4. Ключи указывают на статью, отсутствующую в бандле'
if ($manifest) {
    $inBundle = @{}
    foreach ($t in $manifest.topics) { $inBundle[$t.slug] = $t }
    $missing = @($registry | Where-Object { $_.Slug -and -not $inBundle.ContainsKey($_.Slug) })
    if ($missing.Count -eq 0) { Write-Ok 'все слаги реестра есть в бандле' }
    else {
        # Это штатное состояние, а не дефект: статьи вне узла RootSlug в бандл
        # не попадают по замыслу, и такие кнопки открывают браузер. Настоящую
        # проблему — статья в узле есть, а в бандле нет — ловит секция 6,
        # поэтому здесь только сводка, иначе она забивает отчёт.
        Write-Host ("  · {0} ключей ведут на статьи вне узла {1} — откроют браузер:" -f $missing.Count, $RootSlug) -ForegroundColor DarkYellow
        $notes++
        $names = ($missing | ForEach-Object { $_.Key }) -join ', '
        Write-Host ("      " + $names) -ForegroundColor DarkGray
    }
}

# --- 5. свежесть бандла -------------------------------------------------------
Write-Section '5. Бандл не отстал от вики'
if ($manifest -and -not $NoNetwork) {
    $stale = 0
    foreach ($t in $manifest.topics) {
        try {
            $page = (Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/public/wiki/page/' + [uri]::EscapeDataString($t.slug)) `
                                       -Headers @{ Accept = 'application/json' } -TimeoutSec 30).page
        }
        catch { Write-Problem ("{0}: страница не отдаётся ({1})" -f $t.slug, $_.Exception.Message); continue }

        $wiki   = [datetime]$page.updatedAt
        $bundle = [datetime]$t.updatedAt
        if ($wiki -gt $bundle.AddSeconds(1)) {
            $stale++
            Write-Problem ("«{0}»: в вики {1:dd.MM HH:mm}, в бандле {2:dd.MM HH:mm} — перегенерируйте бандл" -f
                           $t.title, $wiki.ToLocalTime(), $bundle.ToLocalTime())
        }
        Start-Sleep -Milliseconds 120   # вики за свои же запросы отвечает 503, если частить
    }
    if ($stale -eq 0) { Write-Ok 'все статьи бандла свежие' }
}
elseif ($NoNetwork) { Write-Host '  (пропущено: -NoNetwork)' -ForegroundColor DarkGray }

# --- 6. новые статьи в вики, которых нет в бандле ------------------------------
Write-Section ("6. В узле {0} появились статьи, не попавшие в бандл" -f $RootSlug)
if (-not $NoNetwork) {
    try {
        $tree = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/public/wiki/tree') `
                                  -Headers @{ Accept = 'application/json' } -TimeoutSec 30
        $node = @($tree.pages | Where-Object { $_.parentSlug -eq $RootSlug })
        $have = @{}
        if ($manifest) { foreach ($t in $manifest.topics) { $have[$t.slug] = $true } }
        $fresh = @($node | Where-Object { -not $have.ContainsKey($_.slug) })
        if ($fresh.Count -eq 0) { Write-Ok 'бандл содержит все статьи узла' }
        else { foreach ($p in $fresh) { Write-Problem ("«{0}» ({1}) есть в вики, но не в бандле" -f $p.title, $p.slug) } }
    }
    catch { Write-Problem ("не удалось получить дерево вики: " + $_.Exception.Message) }
}
else { Write-Host '  (пропущено: -NoNetwork)' -ForegroundColor DarkGray }

# --- 7. что ещё живёт на старом портале ---------------------------------------
Write-Section '7. Ключи с прямой ссылкой в обход вики'
$direct = @($registry | Where-Object { $_.DirectUrl })
if ($direct.Count -eq 0) { Write-Ok 'все ключи ведут в вики' }
else { foreach ($t in $direct) { Write-Note ("«{0}» -> {1}" -f $t.Key, $t.DirectUrl) } }

# --- 8. функции вообще без статьи ---------------------------------------------
Write-Section '8. Ключи без статьи (ведут в корень вики)'
$none = @($registry | Where-Object { $_.Key -ne '-' -and -not $_.Slug -and -not $_.DirectUrl })
if ($none.Count -eq 0) { Write-Ok 'у всех функций есть статья' }
else { Write-Note (($none | ForEach-Object { $_.Key }) -join ', ') }

# --- 9. порядок разделов в панели совпадает с лентой ---------------------------
Write-Section '9. Порядок разделов в панели соответствует ленте'
if ($manifest) {
    $expected = Get-SlugRibbonOrder -Registry $registry -RibbonOrder $ribbon
    $actual = @($manifest.topics | Sort-Object sort, title | ForEach-Object { $_.slug })
    $wanted = @($manifest.topics |
        Sort-Object @{ Expression = { if ($expected.ContainsKey($_.slug)) { $expected[$_.slug] } else { [int]::MaxValue } } }, title |
        ForEach-Object { $_.slug })
    if (($actual -join ',') -eq ($wanted -join ',')) { Write-Ok 'порядок совпадает' }
    else {
        Write-Problem 'порядок в бандле не совпадает с порядком кнопок — перегенерируйте бандл'
        $byS = @{}; foreach ($t in $manifest.topics) { $byS[$t.slug] = $t.title }
        Write-Host ('      сейчас: ' + (($actual | ForEach-Object { $byS[$_] }) -join ' | ')) -ForegroundColor DarkGray
        Write-Host ('      надо:   ' + (($wanted | ForEach-Object { $byS[$_] }) -join ' | ')) -ForegroundColor DarkGray
    }
}

# ---------------------------------------------------------------------------
Write-Host ''
if ($problems -eq 0) {
    Write-Host ("Расхождений нет. Замечаний: {0}." -f $notes) -ForegroundColor Green
    exit 0
}
Write-Host ("Расхождений: {0}. Замечаний: {1}." -f $problems, $notes) -ForegroundColor Red
exit 1
