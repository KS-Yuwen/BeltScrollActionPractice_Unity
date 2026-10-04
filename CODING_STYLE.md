# このプロジェクトの C# コーディング規約

Unity の公式スタイルガイドと Microsoft の C# ガイドを参考に、以下に統一します。
これらはプロジェクトで選択した規約です。すべての公式ガイドを一律に適用するという意味ではありません。
Unity が対応する C# の範囲で実装し、最新の .NET 専用構文は無条件には採用しません。

- クラス、メソッド、公開プロパティ：PascalCase。
- 非公開のインスタンスフィールド：_camelCase。非公開の static フィールド：s_camelCase。
- 引数、ローカル変数：camelCase。型が明らかな場合に限り var を使います。
- アクセス修飾子を明記し、1 行に 1 変数／1 文を記載します。
- インデントは半角スペース 4 個。制御文の本体には波括弧を付け、波括弧を改行します。
- MonoBehaviour は 1 ファイル 1 クラスとし、ファイル名とクラス名を一致させます。
- HP などの状態は読み取り専用の公開プロパティで参照し、変更はメソッドに集約します。
- コメントは日本語で多めに記載し、処理の目的、座標、時間、Unity の仕組みを説明します。
- コメントは原則として説明対象の直前の独立した行に記載します。
- 既存スクリプトの .meta は保持し、シーンからの参照を維持します。

.editorconfig で対応 IDE に命名・書式を通知します。Unity のコンパイルだけで全項目が自動検証されるわけではありません。

参考：
- https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity
- https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
- https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
