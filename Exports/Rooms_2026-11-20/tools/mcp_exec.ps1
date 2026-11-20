param([string]$File, [string]$Tool = "execute_code", [string]$ArgsJson = "", [int]$Timeout = 180)
# Runs a C# method body file through MCP for Unity's execute_code (or any tool with -Tool/-ArgsJson).
$sp = "K:\WORK_2026\08_04\_claude\unity_mcp"
$url = "http://127.0.0.1:8080/mcp"
$sf = Join-Path $sp "mcp_session.txt"
$h = @{ "Accept" = "application/json, text/event-stream" }
function Init {
    $init = '{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"claude-cli","version":"1.0"}}}'
    $h0 = @{ "Accept" = "application/json, text/event-stream" }
    $r = Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -Headers $h0 -ContentType "application/json" -Body $init -TimeoutSec 20
    $sid = $r.Headers["mcp-session-id"]
    Set-Content -Path $sf -Value $sid -NoNewline
    $h2 = $h0.Clone(); $h2["mcp-session-id"] = $sid
    Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -Headers $h2 -ContentType "application/json" -Body '{"jsonrpc":"2.0","method":"notifications/initialized"}' -TimeoutSec 20 | Out-Null
    return $sid
}
if (Test-Path $sf) { $sid = Get-Content $sf -Raw } else { $sid = Init }
if ($File) {
    $code = [IO.File]::ReadAllText($File)
    $arguments = @{ action = "execute"; code = $code; safety_checks = $false }
} else {
    $arguments = $ArgsJson | ConvertFrom-Json
}
$body = @{ jsonrpc = "2.0"; id = (Get-Random); method = "tools/call"; params = @{ name = $Tool; arguments = $arguments } } | ConvertTo-Json -Depth 20 -Compress
$h["mcp-session-id"] = $sid
try {
    $r = Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -Headers $h -ContentType "application/json; charset=utf-8" -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec $Timeout
} catch {
    $sid = Init; $h["mcp-session-id"] = $sid
    $r = Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -Headers $h -ContentType "application/json; charset=utf-8" -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec $Timeout
}
$text = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
foreach ($line in $text -split "`n") {
    if ($line.StartsWith("data: ")) {
        $j = $line.Substring(6) | ConvertFrom-Json
        if ($j.result -and $j.result.content) { foreach ($c in $j.result.content) { $c.text } }
        elseif ($j.error) { "ERROR: " + ($j.error | ConvertTo-Json -Depth 5) }
        else { $line }
    }
}

