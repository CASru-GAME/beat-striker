param (
    [Parameter(Mandatory=$true)]
    [string]$Path
)

if (-not (Test-Path $Path)) {
    Write-Error "File not found: $Path"
    return
}

$bytes = [System.IO.File]::ReadAllBytes($Path)

# UTF-8 BOM: 0xEF, 0xBB, 0xBF
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    Write-Host "File already has UTF-8 BOM: $Path" -ForegroundColor Yellow
} else {
    $bom = [byte[]](0xEF, 0xBB, 0xBF)
    $newBytes = $bom + $bytes
    [System.IO.File]::WriteAllBytes($Path, $newBytes)
    Write-Host "Added UTF-8 BOM to: $Path" -ForegroundColor Green
}
