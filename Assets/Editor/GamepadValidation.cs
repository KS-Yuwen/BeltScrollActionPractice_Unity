using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// 実機を接続していなくても、仮想デバイスに入力イベントを送り操作の割り当てを確認します。
// 通信方式や実機固有の認識はこの検証には含まれず、最後に実機での確認が必要です。
public static class GamepadValidation
{
    public static void RunChecks(Fighter player)
    {
        // バッチモードにはフォーカスされた Game View がないため、検証中だけ入力をゲーム側へ送ります。
        InputSettings.BackgroundBehavior previousBackground = InputSystem.settings.backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            SendState(pad, new GamepadState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            Require(BrawlerInput.IsGamepadConnected, "接続したパッドを認識");
            Require(BrawlerInput.ReadMovement() == Vector3.zero, "ニュートラルでは停止");

            SendState(pad, new GamepadState { leftStick = new Vector2(0.05f, 0) });
            Require(BrawlerInput.ReadMovement() == Vector3.zero, "微小入力をデッドゾーンで除去");
            SendState(pad, new GamepadState { leftStick = new Vector2(0.5f, 0) });
            float partialSpeed = BrawlerInput.ReadMovement().magnitude;
            Require(partialSpeed > 0 && partialSpeed < 1, "浅い入力で低速移動");
            // 読み取り関数だけでなく、実際のキャラクター更新まで入力が届くことを確認します。
            Vector3 initialPosition = player.transform.position;
            UpdatePlayer(player);
            Require(player.transform.position.x > initialPosition.x, "スティック入力がキャラクターの移動へ到達");

            SendState(pad, new GamepadState().WithButton(GamepadButton.DpadUp));
            Require(BrawlerInput.ReadMovement().z > 0.9f, "十字キーの上を奥行きへ変換");
            SendState(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            UpdatePlayer(player);
            Require(player.IsGuarding, "左ショルダーを押している間ガード");
            int guardedHealth = player.Health;
            player.TakeDamage(10, -player.Facing);
            Require(player.Health == guardedHealth, "正面の攻撃をガード");
            Require(player.CanCounter, "防御成功時だけ反撃受付が開く");
            SendState(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder).WithButton(GamepadButton.West));
            UpdatePlayer(player);
            Require(player.IsCounterAttacking && !player.CanCounter && !player.IsGuarding,
                "ガードを押したまま反撃し、受付を一度だけ消費");
            // 防御姿勢に戻して、背面からの攻撃も引き続き検証します。
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            SendState(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            UpdatePlayer(player);
            player.TakeDamage(10, -player.Facing);
            SetPlayerTimer(player, "_counterWindowRemaining", 0);
            SendState(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder).WithButton(GamepadButton.West));
            UpdatePlayer(player);
            Require(player.IsGuarding && !player.IsCounterAttacking, "受付時間終了後はガード中に反撃できない");
            player.TakeDamage(10, player.Facing);
            Require(player.Health == guardedHealth - 10 && !player.IsGuarding, "背後の攻撃は防げない");
            Require(!player.CanCounter, "被ダメージで反撃受付を解除");
            // 次の検証に被ダメージ硬直を持ち越さないようにします。
            player.RestoreHealth(10);
            SetPlayerTimer(player, "_stunRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);

            // 反撃検証で押した攻撃ボタンを離し、次の通常攻撃を新しい押下として送ります。
            SendState(pad, new GamepadState());
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            Require(BrawlerInput.WasAttackPressed(), "左側のフェイスボタンで攻撃");
            UpdatePlayer(player);
            Require(ReadPlayerTimer(player, "_attackRemaining") > 0, "パッドの攻撃入力が戦闘処理へ到達");
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            Require(!BrawlerInput.WasAttackPressed(), "押しっぱなしでは攻撃を再入力しない");
            // コンボの後隙では入力を覚えるだけにし、硬直が終わると次の段が出ることを確認します。
            SendState(pad, new GamepadState());
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            UpdatePlayer(player);
            Require(player.ComboStep == 1, "攻撃中は次の段を即座に発動しない");
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            SendState(pad, new GamepadState());
            UpdatePlayer(player);
            Require(player.ComboStep == 2, "先行入力した攻撃が次の段へつながる");
            SendState(pad, new GamepadState().WithButton(GamepadButton.South));
            Require(BrawlerInput.WasDashPressed(), "下側のフェイスボタンで回避");
            UpdatePlayer(player);
            Require(ReadPlayerTimer(player, "_dashRemaining") > 0, "パッドの回避入力が戦闘処理へ到達");
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            UpdatePlayer(player);
            Require(player.IsDashAttacking && ReadPlayerTimer(player, "_dashRemaining") == 0,
                "回避中の攻撃でダッシュ攻撃へ変換し、回避無敵を終了");
            int healthBeforeDashAttackHit = player.Health;
            player.TakeDamage(10, 1);
            Require(player.Health == healthBeforeDashAttackHit - 10 && !player.IsDashAttacking,
                "ダッシュ攻撃は被ダメージで中断される");
            player.RestoreHealth(10);
            SetPlayerTimer(player, "_stunRemaining", 0);
            SendState(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            Require(BrawlerInput.WasDashPressed(), "右ショルダーボタンでも回避");
            SendState(pad, new GamepadState().WithButton(GamepadButton.Start));
            Require(BrawlerInput.WasRestartPressed(), "Start でリスタート");
            SendState(pad, new GamepadState().WithButton(GamepadButton.North));
            Require(BrawlerInput.WasHealPressed(), "Y・三角ボタンで回復入力");
            SendState(pad, new GamepadState().WithButton(GamepadButton.North));
            Require(!BrawlerInput.WasHealPressed(), "回復ボタンの長押しでは再入力しない");

            SendState(pad, new GamepadState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.J, Key.LeftShift, Key.R));
            InputSystem.Update();
            Require(Mathf.Approximately(BrawlerInput.ReadMovement().magnitude, 1), "キーボードの斜め移動は速度を制限");
            Require(BrawlerInput.WasAttackPressed() && BrawlerInput.WasDashPressed() && BrawlerInput.WasRestartPressed(), "キーボードの操作も維持");
            SetPlayerTimer(player, "_dashCooldown", 0);
            UpdatePlayer(player);
            Require(player.IsDashAttacking, "キーボードの回避と攻撃の同時押しでもダッシュ攻撃");

            InputSystem.RemoveDevice(pad);
            Require(BrawlerInput.ReadMovement().sqrMagnitude > 0, "パッドを取り外してもキーボード移動が可能");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.L));
            InputSystem.Update();
            Require(BrawlerInput.WasHealPressed(), "L キーで回復入力");
            Debug.Log("GAMEPAD_VALIDATION_PASSED");
        }
        finally
        {
            // テスト用デバイスを必ず片付け、実機側の入力を変更したままにしません。
            if (pad.added)
            {
                InputSystem.RemoveDevice(pad);
            }
            InputSystem.RemoveDevice(keyboard);
            InputSystem.Update();
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
        }
    }

    private static void SendState(Gamepad pad, GamepadState state)
    {
        InputSystem.QueueStateEvent(pad, state);
        InputSystem.Update();
    }

    private static void UpdatePlayer(Fighter player)
    {
        player.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
    }

    private static float ReadPlayerTimer(Fighter player, string fieldName)
    {
        return (float)typeof(Fighter).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
    }

    private static void SetPlayerTimer(Fighter player, string fieldName, float value)
    {
        typeof(Fighter).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"パッド検証失敗：{message}");
        }
    }
}
