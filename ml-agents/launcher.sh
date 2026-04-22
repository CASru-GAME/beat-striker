#!/usr/bin/env bash

# Beat Striker ML-Agents 対話型ランチャースクリプト
# 実行するディレクトリを自分自身の場所に移動
cd "$(dirname "$0")"

# MacOS環境などでローカルのvenv(mlagents-env)が存在すれば自動でアクティベート
if [ -f "mlagents-env/bin/activate" ]; then
    echo "ローカルの Python 仮装環境 (mlagents-env) をアクティベートします..."
    source mlagents-env/bin/activate
fi

echo "========================================="
echo "    Beat Striker ML-Agents Launcher"
echo "========================================="
echo "【学習モードの選択】"
echo "  1) 基本学習（サンドバッグ）: Satan.yaml"
echo "  2) セルフプレイ学習（AI対戦）: Satan_SelfPlay.yaml"
read -p "番号を入力してください [1 または 2]: " mode_selection

yaml_file="Satan.yaml"
if [ "$mode_selection" == "2" ]; then
    yaml_file="Satan_SelfPlay.yaml"
fi

echo "-----------------------------------------"
echo "【実行アクションの選択】"
echo "  1) 新規学習スタート"
echo "  2) 同じ設定で学習を再開 (--resume)"
echo "  3) 過去の脳を受け継いで新規スタート (--initialize-from)"
read -p "番号を入力してください [1, 2, 3 のいずれか]: " action_selection

echo "-----------------------------------------"
read -p "今回の学習の名前 (Run ID) を入力してください (例: Train_01): " run_id

# コマンドの組み立て
command="mlagents-learn $yaml_file --run-id=$run_id"

if [ "$action_selection" == "2" ]; then
    command="$command --resume"
elif [ "$action_selection" == "3" ]; then
    echo ""
    read -p "★ 受け継ぎ元となる過去の Run ID を入力してください (例: Train_Base): " prev_run_id
    command="$command --initialize-from=$prev_run_id"
fi

echo "========================================="
echo "以下のコマンドを実行します:"
echo "> $command"
echo "========================================="

# 実行
eval $command
