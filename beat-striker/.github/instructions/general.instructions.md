---
applyTo: '**'
---

# General Instructions

生成AIは、Assets/Scripts 以下のスクリプトは許可なく変更しないこと。もし変更する場合は厳重に確認すること。
シーンの遷移は、直接スクリプトにシーン名を書き込むのではなく、バスのPublish(AppMessages.RequireTransition)を使うこと。
シーン内で実行されるスクリプトのバスは、this.GetBus()で取得できる。