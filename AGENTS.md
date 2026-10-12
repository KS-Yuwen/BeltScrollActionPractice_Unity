# Repository Guidelines

## プロジェクト構成と役割

Unity 6000.6.4f1 用の3Dベルトスクロールアクションの試作です。実行するシーンは `Assets/Scenes/NeonStreet.unity` です。

- `Assets/Scripts/`：実行時のC#コード。`BeltBrawler` は進行・戦闘対象、`BrawlerSceneFactory` はモデル・素材生成、`BrawlerHud` は画面表示を担当します。
- `Combatant` と `Fighter`：キャラクターの共通処理。職業固有の能力やアイテム使用許可は `ClericFighter` などの派生クラスに実装します。
- `PlayerThrownProjectile`：投擲物の寿命・命中判定を共通化し、ナイフ・ハンマーの派生クラスが軌道と効果を定義します。
- `Assets/Editor/`：シーン生成・入力設定・自動検証ツール。
- `Assets/Resources/Balance/slide_balance.csv`：コマンド技の調整値。`SlideBalanceSettings` と `BrawlerBalance` を通して参照します。
- `Packages/`・`ProjectSettings/`：依存パッケージとUnityの設定。
- `docs/`：ローカルのコーディング規約・操作実装計画。Git管理から除外します。
- `.codex/`：参考動画などのローカル作業メモ。Git管理から除外します。

## 開発・ビルド・検証コマンド

Unity Hubでこのフォルダを開き、対象シーンでPlayを押してください。実行ファイルのビルドにはUnityのBuild Profilesを使用します。ビルド用の自動コマンドはありません。

自動検証はプロジェクトのルートで実行します。同じプロジェクトを開いている別のUnityは閉じてください。

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
& $unity -batchmode -nographics -projectPath "$PWD" -executeMethod CombatValidation.Run -logFile combat-validation.log
```

Playモードで戦闘・パッド入力を検証し、終了します。非同期の検証が途中で終了するため、`-quit` は付けません。入力設定に問題がある場合は `Tools/Neon Street/Apply Input Settings and Restart` を使用します。

## コーディング規約と命名

`.editorconfig` と、存在すれば `docs/CODING_STYLE.md` に従います。インデントは半角スペース4個、波括弧は改行し、アクセス修飾子を明記して1行1文とします。

型・公開メンバーは `PascalCase`、非公開インスタンスフィールドは `_camelCase`、非公開staticフィールドは `s_camelCase` にします。MonoBehaviourのファイル名とクラス名は一致させます。

コメントは日本語で多めに記載し、目的・座標・時間・Unity固有の仕組みを説明してください。ゲーム調整値はマジックナンバーにせず、名前付き定数へ集約します。既存の `.meta` のGUIDを維持し、新規アセットには重複しないGUIDを用意します。

## テスト方針

操作・戦闘の参考資料を確認する際は、存在すれば `.codex/REFERENCE_VIDEOS.md` の動画一覧と参照位置を確認してください。ローカル資料のため、別の環境には存在しない場合があります。

独自のエディター検証ツールを使用します。ログに `COMBAT_VALIDATION_PASSED` と `GAMEPAD_VALIDATION_PASSED` の両方があることを確認してください。カバレッジ率の指定はありません。

項目別の検証メソッドは `Validate<対象動作>` とし、アサーションには具体的な説明を付けます。変更に応じてキーボード・パッド入力、左右の向き、アイテム制限、時間経過を検証し、見た目と操作感は実機で確認します。

## コミットとプルリクエスト

履歴では `敗北時リザルト追加。` のような日本語の変更要約を使用しています。Conventional Commits形式は必須ではありません。コミットは目的ごとにまとめてください。

PRには動作の変更点・検証結果・残る制約を記載し、関連Issueがあればリンクします。画面を変更した場合はスクリーンショットを添付してください。Unityの生成フォルダやログは含めず、操作・調整方法が変わる場合はREADMEも更新します。
