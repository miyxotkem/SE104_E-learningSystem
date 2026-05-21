$line = 'CustomDialog.Show(sách táº£i danh sÃ¡ch video: " + ex.Message, "Lá»—i", DialogType.Error);'
$win1252 = [System.Text.Encoding]::GetEncoding(1252, [System.Text.EncoderFallback]::ExceptionFallback, [System.Text.DecoderFallback]::ExceptionFallback)
$strictUtf8 = New-Object System.Text.UTF8Encoding $false, $true
try {
    $bytes = $win1252.GetBytes($line)
    Write-Output $strictUtf8.GetString($bytes)
} catch {
    Write-Output "Error: $_"
}
