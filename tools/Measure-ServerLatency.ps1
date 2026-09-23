<#
.SYNOPSIS
    Замер задержек до серверной папки TNov: базовая линия перед переездом на единый
    сервер и контроль после оптимизаций.

.DESCRIPTION
    Повторяет типовые обращения плагина к SMB-шаре и печатает медиану/максимум:
      - ping хоста;
      - проверка существования файла (File.Exists);
      - чтение справочника (roles.txt, CDE.txt, RS.txt);
      - листинг папки projects\;
      - (с -WriteTest) дозапись строки в отдельный тестовый файл, как делал usage.txt.

    По умолчанию берёт ServerPath из %USERPROFILE%\TNovClient\TNovConfig.json.

.EXAMPLE
    .\Measure-ServerLatency.ps1
    .\Measure-ServerLatency.ps1 -ServerPath '\\fs-nova\Distr\0.For Admin\_TNov\' -Iterations 20 -WriteTest
#>
param(
    [string]$ServerPath,
    [int]$Iterations = 10,
    [switch]$WriteTest
)

$ErrorActionPreference = 'Stop'

if (-not $ServerPath) {
    $configPath = Join-Path $env:USERPROFILE 'TNovClient\TNovConfig.json'
    $ServerPath = (Get-Content $configPath -Raw | ConvertFrom-Json).ServerPath
}
$ServerPath = $ServerPath.Replace('/', '\')
if (-not $ServerPath.EndsWith('\')) { $ServerPath += '\' }
$serverHost = ($ServerPath.TrimStart('\') -split '\\')[0]

function Measure-Op([string]$name, [scriptblock]$op) {
    $times = @()
    $err = $null
    for ($i = 0; $i -lt $Iterations; $i++) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        try { & $op | Out-Null } catch { $err = $_.Exception.Message }
        $sw.Stop()
        $times += $sw.Elapsed.TotalMilliseconds
    }
    # Первое обращение — реалистичная цена для плагина: SMB-клиент Windows кэширует
    # метаданные и содержимое ~10 с, поэтому повторы подряд почти бесплатны, а команды
    # пользователя разнесены во времени и попадают на «холодный» кэш.
    $sorted = $times | Sort-Object
    [pscustomobject]@{
        'Операция'   = $name
        'Первое, мс'  = [math]::Round($times[0], 1)
        'Медиана, мс' = [math]::Round($sorted[[int][math]::Floor($sorted.Count / 2)], 1)
        'Макс, мс'    = [math]::Round(($sorted | Measure-Object -Maximum).Maximum, 1)
        'Ошибка'     = $err
    }
}

Write-Host "Сервер: $ServerPath  (хост $serverHost), итераций: $Iterations, машина: $env:COMPUTERNAME"

$results = @()

$ping = Test-Connection -ComputerName $serverHost -Count ([math]::Min($Iterations, 10)) -ErrorAction SilentlyContinue
if ($ping) {
    $rtt = $ping | ForEach-Object { if ($_.PSObject.Properties['Latency']) { $_.Latency } else { $_.ResponseTime } }
    $results += [pscustomobject]@{
        'Операция' = 'ping'; 'Первое, мс' = $rtt[0]; 'Медиана, мс' = ($rtt | Sort-Object)[[int][math]::Floor($rtt.Count / 2)]
        'Макс, мс' = ($rtt | Measure-Object -Maximum).Maximum; 'Ошибка' = $null
    }
}

$results += Measure-Op 'File.Exists(usage.txt)' { [IO.File]::Exists($ServerPath + 'usage.txt') }
foreach ($f in 'roles.txt', 'CDE.txt', 'RS.txt') {
    $results += Measure-Op "ReadAllText($f)" { [IO.File]::ReadAllText($ServerPath + $f) }
}
$results += Measure-Op 'Directory.Exists(projects)' { [IO.Directory]::Exists($ServerPath + 'projects') }
$results += Measure-Op 'GetFiles(projects\*checklist*)' { [IO.Directory]::GetFiles($ServerPath + 'projects', '*checklist*') }

if ($WriteTest) {
    # Отдельный файл, чтобы не засорять usage.txt; удаляется в конце.
    $testFile = $ServerPath + "logs\latency-$env:COMPUTERNAME.txt"
    $results += Measure-Op 'AppendAllText(logs\latency-*.txt)' {
        [IO.File]::AppendAllText($testFile, "`n$(Get-Date -Format o),$env:USERNAME")
    }
    Remove-Item $testFile -ErrorAction SilentlyContinue
}

$results | Format-Table -AutoSize
