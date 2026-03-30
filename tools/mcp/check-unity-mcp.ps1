param(
  [string]$BaseUrl = "http://127.0.0.1:43157"
)

$ErrorActionPreference = "Stop"

$healthUrl = "$BaseUrl/health"
$mcpUrl = "$BaseUrl/mcp"

try {
  $health = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 5
  Write-Output "Unity MCP healthy"
  Write-Output "Health URL: $healthUrl"
  Write-Output "MCP URL: $mcpUrl"
  $health | ConvertTo-Json -Depth 8
} catch {
  Write-Error "Unity MCP health check failed for $healthUrl. $($_.Exception.Message)"
  exit 1
}
