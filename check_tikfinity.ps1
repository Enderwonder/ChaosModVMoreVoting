Write-Host "Monitoring TikFinity WebSocket port (21213)..."
Write-Host "Current time: $(Get-Date -Format 'HH:mm:ss')"

$lastState = $null

while ($true) {
    $result = Test-NetConnection -ComputerName localhost -Port 21213 -WarningAction SilentlyContinue
    $currentTime = Get-Date -Format "HH:mm:ss"
    
    if ($result.TcpTestSucceeded) {
        if ($lastState -ne "open") {
            Write-Host "`nPort 21213 is OPEN at: $currentTime"
            Write-Host "TikFinity WebSocket should be accessible"
            $lastState = "open"
        }
        Write-Host "." -NoNewline
    }
    else {
        if ($lastState -ne "closed") {
            Write-Host "`nPort 21213 is CLOSED at: $currentTime"
            Write-Host "TikFinity might not be running"
            $lastState = "closed"
        }
        Write-Host "x" -NoNewline
    }
    
    Start-Sleep -Seconds 1
}
