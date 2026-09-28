<#
.SYNOPSIS
    Генерирует значки ленты для тёмной темы Revit (resources\dark\*.png).

.DESCRIPTION
    Берёт все PNG из resources\ (и значки SchemeBuilder из TNovSS) и перекрашивает:
    - серые/чёрные пиксели (контуры) — светлеют: яркость L -> max(L, 0.92 * (1 - L));
    - цветные пиксели — оттенок и насыщенность сохраняются, тёмные цвета поднимаются по яркости;
    - альфа не меняется.
    Уже существующие файлы в dark\ не трогаются (их могли дорисовать вручную) — для перезаписи -Force.
    Скрипт запускается вручную, результат коммитится. В сборку не входит.

.EXAMPLE
    pwsh -File tools\MakeDarkIcons.ps1
    pwsh -File tools\MakeDarkIcons.ps1 -Only tnovproq32,tnovproq16 -Force
#>
param(
    [string[]]$Only,
    [switch]$Force
)

Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $root 'resources\dark'
New-Item -ItemType Directory -Force $outDir | Out-Null

# Источник -> префикс имени в dark\ (префикс совпадает с ключом в RibbonIcons).
$sources = @(
    @{ Dir = Join-Path $root 'resources'; Prefix = '' },
    @{ Dir = Join-Path $root '..\TNovSS\SchemeBuilder\src\SchemeBuilder\Resources'; Prefix = 'SchemeBuilder.' }
)

function ConvertTo-Hsl([double]$r, [double]$g, [double]$b) {
    $max = [Math]::Max($r, [Math]::Max($g, $b)); $min = [Math]::Min($r, [Math]::Min($g, $b))
    $l = ($max + $min) / 2; $h = 0.0; $s = 0.0
    if ($max -ne $min) {
        $d = $max - $min
        $s = if ($l -gt 0.5) { $d / (2 - $max - $min) } else { $d / ($max + $min) }
        if ($max -eq $r) { $h = ($g - $b) / $d + $(if ($g -lt $b) { 6 } else { 0 }) }
        elseif ($max -eq $g) { $h = ($b - $r) / $d + 2 }
        else { $h = ($r - $g) / $d + 4 }
        $h /= 6
    }
    return $h, $s, $l
}

function Get-HueChannel([double]$p, [double]$q, [double]$t) {
    if ($t -lt 0) { $t += 1 }; if ($t -gt 1) { $t -= 1 }
    if ($t -lt 1/6) { return $p + ($q - $p) * 6 * $t }
    if ($t -lt 1/2) { return $q }
    if ($t -lt 2/3) { return $p + ($q - $p) * (2/3 - $t) * 6 }
    return $p
}

function ConvertFrom-Hsl([double]$h, [double]$s, [double]$l) {
    if ($s -eq 0) { return $l, $l, $l }
    $q = if ($l -lt 0.5) { $l * (1 + $s) } else { $l + $s - $l * $s }
    $p = 2 * $l - $q
    return (Get-HueChannel $p $q ($h + 1/3)), (Get-HueChannel $p $q $h), (Get-HueChannel $p $q ($h - 1/3))
}

function Convert-Pixel([System.Drawing.Color]$c) {
    $h, $s, $l = ConvertTo-Hsl ($c.R / 255.0) ($c.G / 255.0) ($c.B / 255.0)
    if ($s -lt 0.2) {
        # Контуры: чёрное -> почти белое, светлое остаётся светлым.
        $s = 0
        $l = [Math]::Max($l, 0.92 * (1 - $l))
    }
    elseif ($l -lt 0.6) {
        # Тёмные цвета поднимаем, чтобы не тонули в фоне ленты.
        $l = $l + (0.6 - $l) * 0.5
    }
    $r, $g, $b = ConvertFrom-Hsl $h $s $l
    return [System.Drawing.Color]::FromArgb($c.A, [int][Math]::Round($r * 255), [int][Math]::Round($g * 255), [int][Math]::Round($b * 255))
}

$made = 0; $skipped = 0
foreach ($src in $sources) {
    if (-not (Test-Path $src.Dir)) { continue }
    foreach ($file in Get-ChildItem $src.Dir -Filter '*.png') {
        $name = $src.Prefix + [IO.Path]::GetFileNameWithoutExtension($file.Name)
        if ($Only -and ($Only -notcontains $name)) { continue }
        $target = Join-Path $outDir ($name + '.png')
        if ((Test-Path $target) -and -not $Force) { $skipped++; continue }

        $img = [System.Drawing.Image]::FromFile($file.FullName)
        $bmp = New-Object System.Drawing.Bitmap $img.Width, $img.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $gr = [System.Drawing.Graphics]::FromImage($bmp); $gr.DrawImage($img, 0, 0, $img.Width, $img.Height); $gr.Dispose(); $img.Dispose()

        $cache = @{}
        for ($y = 0; $y -lt $bmp.Height; $y++) {
            for ($x = 0; $x -lt $bmp.Width; $x++) {
                $c = $bmp.GetPixel($x, $y)
                if ($c.A -eq 0) { continue }
                $key = $c.ToArgb()
                if (-not $cache.ContainsKey($key)) { $cache[$key] = Convert-Pixel $c }
                $bmp.SetPixel($x, $y, $cache[$key])
            }
        }
        $bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
        $made++
    }
}
Write-Host "Создано: $made, пропущено (уже есть): $skipped -> $outDir"
