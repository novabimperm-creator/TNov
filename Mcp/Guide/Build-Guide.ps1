<#
  Собирает две версии инструкции из Guide.src.html:
    Guide.html     — корпоративная и личная конфигурация (остаются блоки <!--P--> … <!--/P-->);
    GuideCorp.html — только корпоративная (остаются блоки <!--C--> … <!--/C-->).
  Правьте только Guide.src.html, затем запустите этот скрипт.
#>
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$src = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Guide.src.html'), $utf8)
$src = [regex]::Replace($src, '(?s)^<!--SRC.*?SRC-->\r?\n', '')

function Build([string]$keep, [string]$drop, [string]$name) {
    $text = [regex]::Replace($src, "(?s)<!--$drop-->.*?<!--/$drop-->\r?\n?", '')
    $text = [regex]::Replace($text, "(?m)^<!--/?$keep-->\r?\n", '')
    if ($text -match '<!--/?[PC]-->') { throw "Незакрытый маркер в $name" }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot $name), $text, $utf8)
    Write-Host "Собрано: $name"
}

Build 'P' 'C' 'Guide.html'
Build 'C' 'P' 'GuideCorp.html'
