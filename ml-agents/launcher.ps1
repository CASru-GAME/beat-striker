$ErrorActionPreference = "Stop"

# PythonがYAMLファイルを読み込む際に cp932 エラーになるのを防ぐため、UTF-8 を強制する
$env:PYTHONUTF8 = "1"

# Beat Striker ML-Agents 実行ディレクトリへ移動
Set-Location -Path $PSScriptRoot

# Windows環境でローカルのvenvが存在すれば自動でアクティベート (PowerShell用)
if (Test-Path "mlagents-env\Scripts\Activate.ps1") {
    Write-Host "ローカルの Python 仮装環境 (mlagents-env) をアクティベートします..." -ForegroundColor Cyan
    . "mlagents-env\Scripts\Activate.ps1"
}

Write-Host "========================================="
Write-Host "    Beat Striker ML-Agents Launcher"
Write-Host "========================================="
Write-Host "【学習モードの選択】"
Write-Host "  1) 基本学習（サンドバッグ）: Satan.yaml"
Write-Host "  2) セルフプレイ学習（AI対戦）: Satan_SelfPlay.yaml"
$mode_selection = Read-Host "番号を入力してください [1 または 2]"

$yaml_file = "Satan.yaml"
if ($mode_selection -eq "2") {
    $yaml_file = "Satan_SelfPlay.yaml"
}

Write-Host "-----------------------------------------"
Write-Host "【実行アクションの選択】"
Write-Host "  1) 新規学習スタート"
Write-Host "  2) 同じ設定で学習を再開 (--resume)"
Write-Host "  3) 過去の脳を受け継いで新規スタート (--initialize-from)"
$action_selection = Read-Host "番号を入力してください [1, 2, 3 のいずれか]"

Write-Host "-----------------------------------------"
$run_id = Read-Host "今回の学習の名前 (Run ID) を入力してください (例: Train_01)"

$arguments = @($yaml_file, "--run-id=$run_id")

if ($action_selection -eq "2") {
    $arguments += "--resume"
} elseif ($action_selection -eq "3") {
    Write-Host ""
    $prev_run_id = Read-Host "★ 受け継ぎ元となる過去の Run ID を入力してください (例: Train_Base)"
    $arguments += "--initialize-from=$prev_run_id"
}

Write-Host "========================================="
Write-Host "以下のコマンドを実行します:" -ForegroundColor Magenta
Write-Host "mlagents-learn $($arguments -join ' ')" -ForegroundColor Yellow
Write-Host "========================================="

# コマンドの実行 (実行ポリシーなどの警告回避と確実なプロセス呼び出し)
Invoke-Expression "mlagents-learn $($arguments -join ' ')"

Write-Host "`n処理が完了(または中断)しました。Enterキーを押して終了します..." -ForegroundColor DarkGray
Read-Host
