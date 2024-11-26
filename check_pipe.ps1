Write-Host "Monitoring for ChaosModVVotingPipe..."
while ($true) {
    $pipe = [System.IO.Directory]::GetFiles('\\.\pipe\', 'ChaosModVVotingPipe')
    if ($pipe) {
        Write-Host "Pipe found at: $pipe"
        break
    }
    Start-Sleep -Milliseconds 500
    Write-Host "." -NoNewline
}
