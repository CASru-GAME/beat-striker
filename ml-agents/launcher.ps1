$ErrorActionPreference = "Stop"

# Force UTF-8 to prevent cp932 errors when Python reads YAML files
$env:PYTHONUTF8 = "1"

# Move to the Beat Striker ML-Agents execution directory
Set-Location -Path $PSScriptRoot

# Automatically activate local venv if it exists in a Windows environment (for PowerShell)
if (Test-Path "mlagents-env\Scripts\Activate.ps1") {
    Write-Host "Activating the local Python virtual environment (mlagents-env)..." -ForegroundColor Cyan
    . "mlagents-env\Scripts\Activate.ps1"
}

Write-Host "========================================="
Write-Host "     Beat Striker ML-Agents Launcher"
Write-Host "========================================="
Write-Host "[Select Training Mode]"
Write-Host "  1) Basic Training (Punching Bag): Satan.yaml"
Write-Host "  2) Self-Play Training (AI vs AI): Satan_SelfPlay.yaml"
$mode_selection = Read-Host "Enter a number [1 or 2]"

$yaml_file = "Satan.yaml"
if ($mode_selection -eq "2") {
    $yaml_file = "Satan_SelfPlay.yaml"
}

Write-Host "-----------------------------------------"
Write-Host "[Select Execution Action]"
Write-Host "  1) Start New Training"
Write-Host "  2) Resume Training with Same Settings (--resume)"
Write-Host "  3) Start New Training Inheriting Past Brain (--initialize-from)"
$action_selection = Read-Host "Enter a number [1, 2, or 3]"

Write-Host "-----------------------------------------"
$run_id = Read-Host "Enter a name for this training session (Run ID) (e.g., Train_01)"

Write-Host "-----------------------------------------"
$num_envs = Read-Host "Enter the number of concurrent windows (num-envs) [Default is 1 if left blank]"
if ([string]::IsNullOrWhiteSpace($num_envs)) {
    $num_envs = "1"
}

$arguments = @($yaml_file, "--run-id=$run_id", "--num-envs=$num_envs", "--width=480", "--height=270")

# --- Added Section: Automatically find and use the latest executable ---
Write-Host "-----------------------------------------"
Write-Host "Searching for the latest executable environment..."
$exe_base_path = Join-Path $PSScriptRoot "..\beat-striker\Dist\ML-Scene"

if (Test-Path $exe_base_path) {
    # Sort directories by name descending (works perfectly for YYYY-MM-DD-HH-mm-ss format)
    $latest_dir = Get-ChildItem -Path $exe_base_path -Directory | Sort-Object Name -Descending | Select-Object -First 1
    
    if ($null -ne $latest_dir) {
        $exe_path = Join-Path $latest_dir.FullName "FighterAI.exe"
        if (Test-Path $exe_path) {
            Write-Host "Found latest environment: $($latest_dir.Name)" -ForegroundColor Cyan
            $arguments += "--env=`"$exe_path`""
        } else {
            Write-Host "Warning: FighterAI.exe not found in $($latest_dir.Name). Running in Editor mode." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Warning: No timestamp directories found. Running in Editor mode." -ForegroundColor Yellow
    }
} else {
    Write-Host "Warning: Executable base path not found. Running in Editor mode." -ForegroundColor Yellow
}
# ---------------------------------------------------------------------

if ($action_selection -eq "2") {
    $arguments += "--resume"
} elseif ($action_selection -eq "3") {
    Write-Host ""
    $prev_run_id = Read-Host "* Enter the past Run ID to inherit from (e.g., Train_Base)"
    $arguments += "--initialize-from=$prev_run_id"
}

Write-Host "========================================="
Write-Host "Executing the following command:" -ForegroundColor Magenta
Write-Host "mlagents-learn $($arguments -join ' ')" -ForegroundColor Yellow
Write-Host "========================================="

# Execute the command
Invoke-Expression "mlagents-learn $($arguments -join ' ')"

Write-Host "`nProcess completed (or interrupted). Press Enter to exit..." -ForegroundColor DarkGray
Read-Host