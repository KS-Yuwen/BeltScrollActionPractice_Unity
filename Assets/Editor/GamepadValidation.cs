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
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            Require(BrawlerInput.WasAttackPressed(), "左側のフェイスボタンで攻撃");
            UpdatePlayer(player);
            Require(ReadPlayerTimer(player, "_attackRemaining") > 0, "パッドの攻撃入力が戦闘処理へ到達");
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            Require(!BrawlerInput.WasAttackPressed(), "押しっぱなしでは攻撃を再入力しない");
            SendState(pad, new GamepadState().WithButton(GamepadButton.South));
            Require(BrawlerInput.WasDashPressed(), "下側のフェイスボタンで回避");
            UpdatePlayer(player);
            Require(ReadPlayerTimer(player, "_dashRemaining") > 0, "パッドの回避入力が戦闘処理へ到達");
            SendState(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            Require(BrawlerInput.WasDashPressed(), "右ショルダーボタンでも回避");
            SendState(pad, new GamepadState().WithButton(GamepadButton.Start));
            Require(BrawlerInput.WasRestartPressed(), "Start でリスタート");

            SendState(pad, new GamepadState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.J, Key.LeftShift, Key.R));
            InputSystem.Update();
            Require(Mathf.Approximately(BrawlerInput.ReadMovement().magnitude, 1), "キーボードの斜め移動は速度を制限");
            Require(BrawlerInput.WasAttackPressed() && BrawlerInput.WasDashPressed() && BrawlerInput.WasRestartPressed(), "キーボードの操作も維持");

            InputSystem.RemoveDevice(pad);
            Require(BrawlerInput.ReadMovement().sqrMagnitude > 0, "パッドを取り外してもキーボード移動が可能");
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
        return (float)player.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"パッド検証失敗：{message}");
        }
    }
}
