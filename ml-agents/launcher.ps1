$ErrorActionPreference = "Stop"

# Force UTF-8 to prevent cp932 errors when Python reads YAML files
$env:PYTHONUTF8 = "1"

# Move to the Beat Striker ML-Agents execution directory
Set-Location -Path $PSScriptRoot

function Test-MlAgentsPython {
    param([Parameter(Mandatory = $true)][string]$PythonPath)

    if (-not (Test-Path $PythonPath)) {
        return $false
    }

    & $PythonPath -c "import mlagents.trainers.learn" *> $null
    return ($LASTEXITCODE -eq 0)
}

function Resolve-MlAgentsPython {
    $candidates = @()

    if (-not [string]::IsNullOrWhiteSpace($env:MLAGENTS_PYTHON)) {
        $candidates += $env:MLAGENTS_PYTHON
    }

    $localPython = Join-Path $PSScriptRoot "mlagents-env\Scripts\python.exe"
    $homePython = Join-Path (Join-Path $HOME "ml-agents") "mlagents-env\Scripts\python.exe"

    $candidates += $localPython
    $candidates += $homePython

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-MlAgentsPython -PythonPath $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw @"
Could not find a Python environment with ML-Agents installed.

Checked:
  - MLAGENTS_PYTHON environment variable
  - $localPython
  - $homePython

Install ML-Agents in one of those environments, or set MLAGENTS_PYTHON to the python.exe inside your ML-Agents venv.
"@
}

function Format-ArgumentForDisplay {
    param([Parameter(Mandatory = $true)][string]$Argument)

    if ($Argument -match '\s') {
        return '"' + $Argument.Replace('"', '\"') + '"'
    }

    return $Argument
}

function Start-WindowArrangementJob {
    param(
        [Parameter(Mandatory = $true)][string]$ProcessName,
        [Parameter(Mandatory = $true)][int]$WindowCount,
        [Parameter(Mandatory = $true)][DateTime]$StartedAt
    )

    Start-Job -Name "BeatStrikerWindowArrangement" -ArgumentList $ProcessName, $WindowCount, $StartedAt -ScriptBlock {
        param($processName, $windowCount, $startedAt)

        Add-Type -AssemblyName System.Windows.Forms
        Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class WindowTools
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
"@

        $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        $firstCompleteAt = $null

        for ($attempt = 0; $attempt -lt 90; $attempt++) {
            $windows = @(Get-Process -Name $processName -ErrorAction SilentlyContinue |
                Where-Object { $_.MainWindowHandle -ne 0 -and $_.StartTime -ge $startedAt.AddSeconds(-10) } |
                Sort-Object StartTime, Id |
                Select-Object -First $windowCount)

            $cellWidth = 1
            $cellHeight = 1
            foreach ($window in $windows) {
                $rect = New-Object WindowTools+Rect
                if ([WindowTools]::GetWindowRect($window.MainWindowHandle, [ref]$rect)) {
                    $cellWidth = [Math]::Max($cellWidth, $rect.Right - $rect.Left)
                    $cellHeight = [Math]::Max($cellHeight, $rect.Bottom - $rect.Top)
                }
            }

            $cols = [Math]::Min($windowCount, [Math]::Max(1, [Math]::Floor($area.Width / $cellWidth)))
            for ($index = 0; $index -lt $windows.Count; $index++) {
                $x = $area.X + ($index % $cols) * $cellWidth
                $y = $area.Y + [Math]::Floor($index / $cols) * $cellHeight
                # Move without resizing, changing focus, or changing window stacking order.
                [WindowTools]::SetWindowPos($windows[$index].MainWindowHandle, [IntPtr]::Zero, $x, $y, 0, 0, 0x0015) > $null
            }

            if ($windows.Count -ge $windowCount -and $null -eq $firstCompleteAt) {
                $firstCompleteAt = Get-Date
            }
            if ($null -ne $firstCompleteAt -and ((Get-Date) - $firstCompleteAt).TotalSeconds -ge 20) {
                break
            }
            Start-Sleep -Seconds 1
        }
    } > $null
}

Write-Host "========================================="
Write-Host "     Beat Striker ML-Agents Launcher"
Write-Host "========================================="
$mlAgentsPython = Resolve-MlAgentsPython
Write-Host "Using ML-Agents Python: $mlAgentsPython" -ForegroundColor Cyan

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

$numEnvCount = 0
if (-not [int]::TryParse($num_envs, [ref]$numEnvCount) -or $numEnvCount -lt 1) {
    throw "num-envs must be a positive integer."
}

$arguments = @($yaml_file, "--run-id=$run_id", "--num-envs=$numEnvCount", "--width=480", "--height=270")

# --- Added Section: Automatically find and use the latest executable ---
Write-Host "-----------------------------------------"
Write-Host "Searching for the latest executable environment..."
$exe_base_path = Join-Path $PSScriptRoot "..\beat-striker\Dist\ML-Scene"
$exe_path = $null

if (Test-Path $exe_base_path) {
    # Sort directories by name descending (works perfectly for YYYY-MM-DD-HH-mm-ss format)
    $latest_dir = Get-ChildItem -Path $exe_base_path -Directory | Sort-Object Name -Descending | Select-Object -First 1
    
    if ($null -ne $latest_dir) {
        $exe_path = Join-Path $latest_dir.FullName "FighterAI.exe"
        if (Test-Path $exe_path) {
            Write-Host "Found latest environment: $($latest_dir.Name)" -ForegroundColor Cyan
            $arguments += "--env=$exe_path"
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
if (-not [string]::IsNullOrWhiteSpace($exe_path)) {
    $arguments += @("--env-args", "-screen-fullscreen", "0", "-force-d3d11")
}

Write-Host "`"$mlAgentsPython`" -m mlagents.trainers.learn $((@($arguments) | ForEach-Object { Format-ArgumentForDisplay $_ }) -join ' ')" -ForegroundColor Yellow
Write-Host "========================================="

# Execute the command
$trainingStartedAt = Get-Date
if (-not [string]::IsNullOrWhiteSpace($exe_path)) {
    $processName = [System.IO.Path]::GetFileNameWithoutExtension($exe_path)
    Start-WindowArrangementJob -ProcessName $processName -WindowCount $numEnvCount -StartedAt $trainingStartedAt
}

& $mlAgentsPython -m mlagents.trainers.learn @arguments

Write-Host "`nProcess completed (or interrupted). Press Enter to exit..." -ForegroundColor DarkGray
Read-Host
