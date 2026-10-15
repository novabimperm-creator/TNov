<#
  Мост stdio -> HTTP для коннектора Revit — Claude (плагин TNov).

  Claude Desktop (обычный чат) подключает локальные MCP-серверы только как запускаемую программу
  с обменом через stdin/stdout. Коннектор TNov работает по HTTP внутри Revit (http://localhost:<порт>/mcp).
  Скрипт читает сообщения JSON-RPC построчно из stdin, пересылает их коннектору и пишет ответы в stdout.

  Если Revit с коннектором не запущен:
    - initialize мост отвечает сам, чтобы Claude Desktop не считал сервер сломанным;
    - tools/list возвращает один инструмент revit_status с подсказкой для пользователя;
    - как только коннектор отвечает, мост шлёт notifications/tools/list_changed — Claude перечитает инструменты.

  Скрипт кладёт на диск и прописывает сам плагин: в claude_desktop_config.json (окно «Claude»)
  и в ~/.codex/config.toml с -Client Codex (окно «Codex»; коннектор Codex — адрес /codex),
  и в ~/.qwen/settings.json с -Client Qwen (коннектор Qwen Code — адрес /qwen).
  В stdout пишутся только сообщения протокола; диагностика — в stderr (журнал MCP клиента)
  и в %LOCALAPPDATA%\TNov\mcp\bridge.log: по нему видно, запускает ли клиент мост вообще.
  У приложений из Microsoft Store запись в AppData может уйти в папку пакета
  (%LOCALAPPDATA%\Packages\<пакет>\LocalCache\Local\TNov\mcp) — «Проверить» в Revit ищет и там.
#>
param(
    [int]$Port = 48884,
    [ValidateSet('Claude', 'Codex', 'Qwen')]
    [string]$Client = 'Claude'
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8
[Console]::OutputEncoding = $utf8
$stdin = New-Object System.IO.StreamReader([Console]::OpenStandardInput(), $utf8)
$stdout = New-Object System.IO.StreamWriter([Console]::OpenStandardOutput(), $utf8)
$stdout.AutoFlush = $true

$path = switch ($Client) { 'Codex' { 'codex' } 'Qwen' { 'qwen' } default { 'mcp' } }
$url = "http://localhost:$Port/$path"
$script:revitUp = $false
$script:announcedDown = $false

$logPath = Join-Path $env:LOCALAPPDATA 'TNov\mcp\bridge.log'

function Log([string]$text) {
    [Console]::Error.WriteLine("[tnov-revit bridge] $text")
    # Журнал не должен ломать мост: любые ошибки записи игнорируем.
    try {
        $dir = Split-Path $logPath
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        if ((Test-Path $logPath) -and (Get-Item $logPath).Length -gt 512KB) { Move-Item $logPath "$logPath.old" -Force }
        $line = "{0:yyyy-MM-dd HH:mm:ss} [{1} pid {2}] {3}`r`n" -f (Get-Date), $Client, $PID, $text
        [System.IO.File]::AppendAllText($logPath, $line, $utf8)
    }
    catch { }
}

function Send([string]$json) {
    # Одна строка на сообщение (требование транспорта stdio).
    $stdout.WriteLine($json.Replace("`r", '').Replace("`n", ''))
}

function Post([string]$body, [int]$timeoutMs) {
    $request = [System.Net.HttpWebRequest]::Create($url)
    $request.Method = 'POST'
    $request.ContentType = 'application/json'
    $request.Accept = 'application/json, text/event-stream'
    $request.Proxy = $null
    $request.Timeout = $timeoutMs
    $request.ReadWriteTimeout = $timeoutMs
    $bytes = $utf8.GetBytes($body)
    $stream = $request.GetRequestStream()
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()
    $response = $request.GetResponse()
    try {
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream(), $utf8)
        return @{ Status = [int]$response.StatusCode; Body = $reader.ReadToEnd() }
    }
    finally { $response.Close() }
}

function Test-Revit {
    try {
        $r = Post '{"jsonrpc":"2.0","id":"bridge-ping","method":"ping"}' 3000
        return $r.Status -eq 200
    }
    catch { return $false }
}

function Get-Id([string]$line) {
    # id нужен только для ответа об ошибке; разбор без падения на нестандартном JSON.
    try {
        $message = $line | ConvertFrom-Json
        if ($message.PSObject.Properties.Name -contains 'id') { return $message.id }
    }
    catch { }
    return $null
}

function Get-Method([string]$line) {
    try { return ($line | ConvertFrom-Json).method } catch { return $null }
}

function IdJson($id) {
    if ($null -eq $id) { return 'null' }
    return ($id | ConvertTo-Json -Compress)
}

$downMessage = "Revit с коннектором TNov не отвечает. Откройте Revit, нажмите на ленте TNov кнопку «Нейросетошная» и повторите запрос."

function Local-Response([string]$method, $id) {
    $idJson = IdJson $id
    switch ($method) {
        'initialize' {
            $instructions = ($downMessage + ' Инструменты Revit появятся автоматически, когда коннектор запустится.') | ConvertTo-Json -Compress
            return "{""jsonrpc"":""2.0"",""id"":$idJson,""result"":{""protocolVersion"":""2025-06-18"",""capabilities"":{""tools"":{""listChanged"":true}},""serverInfo"":{""name"":""tnov-revit"",""version"":""bridge""},""instructions"":$instructions}}"
        }
        'tools/list' {
            $description = ('Проверить связь с Revit. Сейчас коннектор не отвечает: ' + $downMessage) | ConvertTo-Json -Compress
            return "{""jsonrpc"":""2.0"",""id"":$idJson,""result"":{""tools"":[{""name"":""revit_status"",""description"":$description,""inputSchema"":{""type"":""object"",""properties"":{}}}]}}"
        }
        'tools/call' {
            $text = $downMessage | ConvertTo-Json -Compress
            return "{""jsonrpc"":""2.0"",""id"":$idJson,""result"":{""content"":[{""type"":""text"",""text"":$text}],""isError"":true}}"
        }
        'ping' {
            return "{""jsonrpc"":""2.0"",""id"":$idJson,""result"":{}}"
        }
        default {
            $text = $downMessage | ConvertTo-Json -Compress
            return "{""jsonrpc"":""2.0"",""id"":$idJson,""error"":{""code"":-32000,""message"":$text}}"
        }
    }
}

function Handle([string]$line) {
    $method = Get-Method $line
    $id = Get-Id $line
    if ($method -eq 'initialize') {
        try { $info = ($line | ConvertFrom-Json).params.clientInfo; Log "initialize от $($info.name) $($info.version)" } catch { Log 'initialize' }
    }
    elseif ($method -eq 'tools/list') { Log 'tools/list' }
    elseif ($method -eq 'tools/call') {
        try { Log "tools/call $(($line | ConvertFrom-Json).params.name)" } catch { Log 'tools/call' }
    }
    try {
        # Скрипты в Revit могут работать до 10 минут — ждём чуть дольше.
        $r = Post $line 660000
        if (-not $script:revitUp) {
            $script:revitUp = $true
            Log "коннектор отвечает на $url"
        }
        if ($r.Status -eq 200 -and $r.Body) { Send $r.Body }
    }
    catch {
        if ($script:revitUp) { Log "коннектор перестал отвечать: $($_.Exception.Message)" }
        $script:revitUp = $false
        $script:announcedDown = $true
        if ($null -ne $id -and $method) { Send (Local-Response $method $id) }
    }
}

Log "запущен, коннектор: $url (PowerShell $($PSVersionTable.PSVersion))"
$script:revitUp = Test-Revit
if ($script:revitUp) { Log 'коннектор в Revit отвечает' }
else { Log 'коннектор пока не отвечает — жду запуска Revit' }

try {
    while ($true) {
        $task = $stdin.ReadLineAsync()
        # Пока ждём сообщения от клиента, раз в 5 секунд проверяем, не запустили ли коннектор.
        while (-not $task.Wait(5000)) {
            if (-not $script:revitUp -and $script:announcedDown -and (Test-Revit)) {
                $script:revitUp = $true
                $script:announcedDown = $false
                Log "коннектор запустился — сообщаю $Client о новых инструментах"
                Send '{"jsonrpc":"2.0","method":"notifications/tools/list_changed"}'
            }
        }
        $line = $task.Result
        if ($null -eq $line) { Log 'клиент закрыл соединение — выход'; break }
        if ($line.Trim().Length -eq 0) { continue }
        Handle $line
    }
}
catch {
    Log "аварийное завершение: $($_.Exception.Message)"
    throw
}
