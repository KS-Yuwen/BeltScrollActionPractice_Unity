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
            SendState(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            Require(BrawlerInput.WasDashPressed(), "右ショルダーボタンで回避");
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
            SendState(pad, new GamepadState().WithButton(GamepadButton.South));
            Require(BrawlerInput.WasJumpPressed() && !BrawlerInput.WasDashPressed(), "パッド下側はジャンプ専用");
            Require(BrawlerInput.IsJumpHeld(), "パッドのジャンプ保持を認識");
            SendState(pad, new GamepadState().WithButton(GamepadButton.Start));
            Require(BrawlerInput.WasRestartPressed(), "Start でリスタート");
            SendState(pad, new GamepadState().WithButton(GamepadButton.North));
            Require(BrawlerInput.WasHealPressed(), "Y・三角ボタンで回復入力");
            SendState(pad, new GamepadState().WithButton(GamepadButton.North));
            Require(!BrawlerInput.WasHealPressed(), "回復ボタンの長押しでは再入力しない");
            SendState(pad, new GamepadState().WithButton(GamepadButton.East));
            Require(BrawlerInput.WasProtectionPressed(), "B・丸ボタンで補助魔法入力");
            SendState(pad, new GamepadState().WithButton(GamepadButton.East));
            Require(!BrawlerInput.WasProtectionPressed(), "補助魔法の長押しでは再入力しない");

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
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I));
            InputSystem.Update();
            Require(BrawlerInput.WasProtectionPressed(), "I キーで補助魔法入力");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U));
            InputSystem.Update();
            Require(BrawlerInput.WasJumpPressed(), "Uキーでジャンプ入力");
            // 通常の Update まで通して、組み合わせ入力と2回押しの分岐を確認します。
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            SetPlayerTimer(player, "_dashRemaining", 0);
            SetPlayerTimer(player, "_dashCooldown", 0);
            SetPlayerTimer(player, "_dashAttackMovementRemaining", 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.U));
            InputSystem.Update();
            // 先の被ダメージ検証による押し戻しを取り除き、しゃがみ入力による移動だけを調べます。
            typeof(Fighter).GetField("_knockbackVelocity", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, Vector3.zero);
            Vector3 beforeCrouch = player.transform.position;
            UpdatePlayer(player);
            Require(player.IsCrouching && !player.IsAirborne && player.transform.position == beforeCrouch,
                "下＋ジャンプは移動やジャンプではなくしゃがみ");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.U, Key.J));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(player.IsCrouchAttacking, "しゃがみ中の攻撃入力で専用攻撃");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            UpdatePlayer(player);
            Require(!player.IsCrouching, "保持を離すとしゃがみ解除");
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            UpdatePlayer(player);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(player.IsAirborne, "1回目のジャンプは通常ジャンプ");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U));
            InputSystem.Update();
            float beforeBackstepFacing = player.Facing;
            UpdatePlayer(player);
            Require(player.IsBackstepping && !player.IsAirborne && player.Facing == beforeBackstepFacing,
                "素早い2回押しは向きを変えずバックステップ");
            SetPlayerTimer(player, "_dashRemaining", 0);
            SetPlayerTimer(player, "_dashCooldown", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            InputSystem.Update();
            UpdatePlayer(player);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.D, Key.U));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(player.IsSliding && !player.IsCrouching && !player.IsAirborne,
                "実際の下→斜め前下＋ジャンプはしゃがみではなくスライディング");
            Vector3 slideStart = player.transform.position;
            typeof(Fighter).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, new object[] { Vector3.zero, 1f });
            Require(!player.IsSliding && player.transform.position.x - slideStart.x > 3,
                "ジャンプ保持では長いスライディング");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            foreach (float expectedFacing in new float[] { -1, 1 })
            {
                SetPlayerTimer(player, "_attackRemaining", 0.1f);
                SetPlayerTimer(player, "_attackCooldown", 0.1f);
                Key directionKey = expectedFacing < 0 ? Key.A : Key.D;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(directionKey, Key.J));
                InputSystem.Update();
                UpdatePlayer(player);
                // 先行入力後に方向を離し、攻撃開始時のフレームには方向入力がない状況を再現します。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                UpdatePlayer(player);
                Transform model = (Transform)typeof(Fighter).GetField("_visual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                Require(player.Facing == expectedFacing && Mathf.Sign(model.localScale.x) == expectedFacing,
                    "左右どちらも先行入力した攻撃方向と見た目が一致");
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                // しゃがみ攻撃でも、方向＋攻撃の同時入力を開始方向に反映します。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.U, directionKey, Key.J));
                InputSystem.Update();
                UpdatePlayer(player);
                Require(player.IsCrouchAttacking && player.Facing == expectedFacing, "しゃがみ攻撃の方向入力を反映");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                UpdatePlayer(player);
            }
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
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
