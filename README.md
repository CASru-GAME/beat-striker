# beat-striker
ビート・ストライカー 音ゲー×格ゲー

起動時に制作クレジットと使用素材を表示します。対戦曲の作者はステージ選択後のBGM選択画面で確認できます。

対戦履歴・リプレイAPIの設置方法: [さくらのレンタルサーバー（FTP / PHP / SQLite）](cloud/sakura/README.md)

## 使用素材とライセンス

ビルド対象シーン、プレハブ、Addressablesの参照から、使用素材と配布元を確認しています。素材の組み込みと、素材ファイルそのものを配布する場合では適用条件が異なります。

| 素材 | クレジット・条件 |
| --- | --- |
| チュートリアルとキャラクター選択のナレーション | **VOICEVOX:ずんだもん**。[VOICEVOX利用規約](https://voicevox.hiroshiba.jp/term/)と[音源利用ガイドライン](https://www.zunko.jp/con_ongen_kiyaku.html)に従って表記 |
| 歓声、ハンドクラップ、電撃音 | [OtoLogic](https://otologic.jp/free/license)（[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/deed.ja)）。ハンドクラップは加工済み |
| ロケット音とUI効果音 | [Mixkit Sound Effects Free License](https://mixkit.co/license/modal/sfxFree/)。ゲームへの組み込みは可。素材単体やソースファイルに含めた再配布は禁止 |
| メニューの決定・キャンセル音、勇者の斬撃音など | [効果音ラボ](https://soundeffect-lab.info/agreement/)。クレジットは任意 |
| 起動時の選択音、キャラクター選択・対戦UIの効果音 | [魔王魂](https://maou.audio/rule/)。魔王魂素材利用規約に基づくクレジットを表示 |
| 魔王の斬撃音 `se_swing12-1.mp3` | [効果音工房](https://umipla.com/battle-new)の[利用規約](https://umipla.com/%E5%88%A9%E7%94%A8%E8%A6%8F%E7%B4%84)に従って表記 |
| タイトルなどのメニューBGM | Alan McKinney / [ZapSplat.com](https://www.zapsplat.com/license-type/standard-license/)。Standard LicenseのBasic利用者に必要なクレジットを表示 |
| Suno表記の対戦曲 | 利用権のあるプランで生成。[Sunoの利用権の説明](https://help.suno.com/en/articles/2416769) |
| Streetシーンの空のマテリアル | Dungeon Masonの[RPG Monster Duo PBR Polyart](https://assetstore.unity.com/packages/3d/characters/creatures/rpg-monster-duo-pbr-polyart-157762)、[Unity Asset Store標準EULA](https://unity.com/legal/as-terms) |
| Anton、Bangers、ADLaM Display、小杉丸 | Anton・Bangers・ADLaM DisplayはSIL Open Font License 1.1、小杉丸はApache License 2.0。著作権表示とライセンス全文は [Fonts](beat-striker/Assets/Project/Fonts) の各フォントフォルダに同梱 |
| 廻想体 ネクストユーピー、アンニャントロマン | [もじワク研究の使用条件](https://moji-waku.com/mj_work_license/)と[アンニャントロマン同梱規約](beat-striker/Assets/Project/Fonts/toroman/readme.txt)。廻想体のフォントデータそのものの無断再配布は禁止 |

### 配布前に確認する素材

- [Infinity（RockCapers）](https://opentracks.com/bgm/detail/7750)と[Discotheque（FLASH☆BEAT）](https://opentracks.com/bgm/detail/13100)は、楽曲のビートに合わせてプレイする対戦曲として使用しています。[OpenTracks（旧DOVA-SYNDROME）のゲーム向け規定](https://opentracks.com/help/articles/game_trpg/)では、音楽が主なコンテンツになる用途は通常の許諾範囲外です。個別許諾は未取得のため、このゲームの使い方を伝え、適用範囲を確認してください。
- 「戦闘っぽい音楽」（Azuno）、チュートリアルの `217_dr_bpm120_4-4_4_pop.mp3`、一部のバトル音声・効果音（`button61.mp3`、`Fusion_01.mp3`、ラウンドコール、魔法使いの効果音など）は、入手元または利用条件を確認できていません。
- 勇者・戦士・魔法使いのモデル参照には [VRM Public License 1.0](https://vrm.dev/licenses/1.0/) の条件が設定されています。勇者と魔法使いはメタデータ上で個人非営利に限定され、勇者と戦士は作者表記が必要です。作者指定の表記と制作クレジットの対応、ゲーム内利用・配布に必要な権利を、モデルの制作者に確認してください。
