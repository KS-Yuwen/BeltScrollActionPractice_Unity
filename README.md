# Neon Street — 3D Belt Brawler Prototype

Unity 6000.6.4f1 用の試作です。Unity Hub でこのフォルダをプロジェクトとして追加します。
`Assets/Scenes/NeonStreet.unity` を開き、Play を押してください。

- WASD / 矢印: 左右・奥行き移動
- J / Space: 通常攻撃（続けて押すと3段コンボ）
- R: リスタート

敵を倒すと次のウェーブが登場します。HP がゼロになると敗北します。
キャラクターは図形を組み合わせた仮モデルです。アニメーション、実モデル、効果音、ゲームパッド対応は今後の作業です。

シーンを再生成する場合は Unity のバッチモードで `-executeMethod CreateDemo.BuildScene` を使用します。
