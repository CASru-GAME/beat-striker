@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: Beat Striker ML-Agents 実行ディレクトリへ移動
cd /d "%~dp0"

:: Windows環境でローカルのvenvが存在すれば自動でアクティベート
if exist "mlagents-env\Scripts\activate.bat" (
    echo ローカルの Python 仮装環境 (mlagents-env) をアクティベートします...
    call mlagents-env\Scripts\activate.bat
)

echo =========================================
echo     Beat Striker ML-Agents Launcher
echo =========================================
echo 【学習モードの選択】
echo   1) 基本学習（サンドバッグ）: Satan.yaml
echo   2) セルフプレイ学習（AI対戦）: Satan_SelfPlay.yaml
set /p mode_selection="番号を入力してください [1 または 2]: "

set yaml_file=Satan.yaml
if "%mode_selection%"=="2" (
    set yaml_file=Satan_SelfPlay.yaml
)

echo -----------------------------------------
echo 【実行アクションの選択】
echo   1) 新規学習スタート
echo   2) 同じ設定で学習を再開 (--resume)
echo   3) 過去の脳を受け継いで新規スタート (--initialize-from)
set /p action_selection="番号を入力してください [1, 2, 3 のいずれか]: "

echo -----------------------------------------
set /p run_id="今回の学習の名前 (Run ID) を入力してください (例: Train_01): "

set command=mlagents-learn !yaml_file! --run-id=!run_id!

if "%action_selection%"=="2" (
    set command=!command! --resume
) else if "%action_selection%"=="3" (
    echo.
    set /p prev_run_id="★ 受け継ぎ元となる過去の Run ID を入力してください (例: Train_Base): "
    set command=!command! --initialize-from=!prev_run_id!
)

echo =========================================
echo 以下のコマンドを実行します:
echo ^> !command!
echo =========================================

call !command!

echo.
pause
