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
            SetPlayerTimer(player, "_guardReleaseRemaining", 0);
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
            // 命中前の追加入力は記憶せず、空振りから次段へ進まないことを確認します。
            SendState(pad, new GamepadState());
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            UpdatePlayer(player);
            Require(player.ComboStep == 1, "攻撃中は次の段を即座に発動しない");
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            SendState(pad, new GamepadState());
            UpdatePlayer(player);
            Require(player.ComboStep == 1 && ReadPlayerTimer(player, "_attackRemaining") <= 0,
                "命中前の入力を持ち越して次段を発動しない");
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

            // 選択は回数を消費せず、押し続けても切り替わり続けません。
            SendState(pad, new GamepadState().WithButton(GamepadButton.Start));
            Require(BrawlerInput.WasPreparationConfirmPressed() && !BrawlerInput.WasRestartPressed(allowGamepad: false),
                "準備中のStartはボス戦開始として受け付け、再開始には使わない");
            SendState(pad, new GamepadState().WithButton(GamepadButton.Start));
            Require(!BrawlerInput.WasPreparationConfirmPressed(), "開始ボタンの保持では再入力しない");
            SendState(pad, new GamepadState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            InputSystem.Update();
            Require(BrawlerInput.WasPreparationConfirmPressed(), "Enterでボス戦開始入力");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            var spellCaster = (ClericFighter)player;
            int healingBeforeSelection = spellCaster.HealingUsesRemaining;
            int protectionBeforeSelection = spellCaster.ProtectionUsesRemaining;
            SendState(pad, new GamepadState().WithButton(GamepadButton.Select));
            Require(BrawlerInput.WasActionSelectPressed(), "View・Shareで魔法選択入力");
            UpdatePlayer(player);
            Require(spellCaster.SelectedAction == BrawlerDefines.UsableAction.Protection, "パッド入力で補助魔法へ切り替え");
            SendState(pad, new GamepadState().WithButton(GamepadButton.Select));
            UpdatePlayer(player);
            Require(!BrawlerInput.WasActionSelectPressed() && spellCaster.SelectedAction == BrawlerDefines.UsableAction.Protection,
                "選択長押しでは連続切り替えしない");
            SendState(pad, new GamepadState { rightTrigger = 1 });
            Require(BrawlerInput.WasActionUsePressed(), "RT・R2で選択魔法の使用入力");
            SendState(pad, new GamepadState { rightTrigger = 1 });
            Require(!BrawlerInput.WasActionUsePressed(), "使用長押しでは再発動しない");
            SendState(pad, new GamepadState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(spellCaster.SelectedAction == BrawlerDefines.UsableAction.AttackBoost, "Qで3枠目の攻撃強化へ切り替え");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(spellCaster.SelectedAction == BrawlerDefines.UsableAction.None, "Qで空の4枠目へ切り替え");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(spellCaster.SelectedAction == BrawlerDefines.UsableAction.None, "Qで空の5枠目へ切り替え");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(spellCaster.SelectedAction == BrawlerDefines.UsableAction.None, "Qで空の6枠目へ切り替え");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(spellCaster.SelectedAction == BrawlerDefines.UsableAction.Healing, "Qで回復へ循環して戻る");
            Require(spellCaster.HealingUsesRemaining == healingBeforeSelection
                && spellCaster.ProtectionUsesRemaining == protectionBeforeSelection, "選択だけでは使用回数を消費しない");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            Require(BrawlerInput.WasActionUsePressed(), "Eで選択魔法の使用入力");
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
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            InputSystem.Update();
            UpdatePlayer(player);
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
            // 直前のしゃがみ攻撃を別ボタン→ジャンプとして扱わず、ジャンプ2回を単独で検証します。
            GetBackstepButtons(player).Reset();
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
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.D));
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
                // 向きの確認は待機状態から行います。未命中の攻撃中は入力を予約しません。
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                // 待機中の「後ろ＋攻撃」はガードなので、前方向の攻撃として左右を別々に確認します。
                typeof(Fighter).GetProperty("Facing").SetValue(player, expectedFacing);
                Key directionKey = expectedFacing < 0 ? Key.A : Key.D;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(directionKey, Key.J));
                InputSystem.Update();
                UpdatePlayer(player);
                // 攻撃開始後に方向を離しても、向きと見た目が一致することを確認します。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                UpdatePlayer(player);
                Transform model = (Transform)typeof(Fighter).GetField("_visual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                Require(player.Facing == expectedFacing && Mathf.Sign(model.localScale.x) == expectedFacing,
                    "左右どちらも入力した攻撃方向と見た目が一致");
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                // しゃがみ攻撃でも、方向＋攻撃の同時入力を開始方向に反映します。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                InputSystem.Update();
                UpdatePlayer(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.U));
                InputSystem.Update();
                UpdatePlayer(player);
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
            foreach (float specialFacing in new float[] { -1, 1 })
            {
                typeof(Fighter).GetProperty("Facing").SetValue(player, specialFacing);
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                SetPlayerTimer(player, "_dashAttackMovementRemaining", 0);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                UpdatePlayer(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                InputSystem.Update();
                UpdatePlayer(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, specialFacing < 0 ? Key.A : Key.D));
                InputSystem.Update();
                UpdatePlayer(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, specialFacing < 0 ? Key.A : Key.D, Key.J));
                InputSystem.Update();
                UpdatePlayer(player);
                Require(player.IsDashSpecialAttacking && !player.IsSliding && player.Facing == specialFacing,
                    "左右の下→斜め前下＋攻撃はダッシュ必殺技へ分岐");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                SetPlayerTimer(player, "_dashAttackMovementRemaining", 0);
                UpdatePlayer(player);
            }
            foreach (float uppercutFacing in new float[] { -1, 1 })
            {
                typeof(Fighter).GetProperty("Facing").SetValue(player, uppercutFacing);
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                InputSystem.Update();
                UpdatePlayer(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                UpdatePlayer(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                UpdatePlayer(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.J));
                InputSystem.Update();
                UpdatePlayer(player);
                Require(player.IsUppercutAttacking && player.Facing == uppercutFacing,
                    "左右の下→上＋攻撃で対空技を発動");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                UpdatePlayer(player);
            }
            foreach (float strikeFacing in new float[] { -1, 1 })
            {
                typeof(Fighter).GetProperty("Facing").SetValue(player, strikeFacing);
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                UpdatePlayer(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(strikeFacing < 0 ? Key.A : Key.D));
                InputSystem.Update();
                UpdatePlayer(player);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(strikeFacing < 0 ? Key.A : Key.D, Key.J));
                InputSystem.Update();
                UpdatePlayer(player);
                Require(player.IsHeavyStriking && player.Facing == strikeFacing, "左右の前＋攻撃で大斬り");
                Require(BrawlerInput.IsAttackHeld(), "攻撃長押しの入力を認識");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                UpdatePlayer(player);
            }
            // ガードからの前入力反撃と解除後の無敵を左右で確認します。
            foreach (float guardFacing in new float[] { -1, 1 })
            {
                typeof(Fighter).GetProperty("Facing").SetValue(player, guardFacing);
                SetPlayerTimer(player, "_counterWindowRemaining", 0);
                Key backward = guardFacing < 0 ? Key.D : Key.A;
                Key forward = guardFacing < 0 ? Key.A : Key.D;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(backward, Key.J));
                InputSystem.Update();
                UpdatePlayer(player);
                Require(player.IsGuarding && player.Facing == guardFacing && ReadPlayerTimer(player, "_attackRemaining") <= 0,
                    "後ろ＋攻撃で向きを固定してガードし、通常攻撃を出さない");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(forward, Key.J));
                InputSystem.Update();
                UpdatePlayer(player);
                Require(player.IsUppercutAttacking && !player.IsGuarding && player.Facing == guardFacing,
                    "左右ともガード中の前入力だけで対空反撃し、向きを固定");
                int commandGuardHealth = player.Health;
                player.TakeDamage(10, -guardFacing);
                Require(player.Health == commandGuardHealth && player.IsUppercutAttacking && !player.CanCounter,
                    "ガードから対空へ移った直後はダメージを受けない");
                typeof(Fighter).GetMethod("UpdateTimers", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, new object[] { BrawlerBalance.GuardReleaseInvulnerabilitySeconds });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                UpdatePlayer(player);
                Require(!player.IsGuarding, "攻撃ボタンを離すとコマンドガード解除");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(backward, Key.J));
                InputSystem.Update();
                UpdatePlayer(player);
                player.TakeDamage(1, guardFacing);
                Require(player.Health == commandGuardHealth - 1 && !player.IsCounterAttacking && !player.IsGuarding,
                    "背後からの攻撃は防げず、自動反撃もしない");
                player.RestoreHealth(1);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                SetPlayerTimer(player, "_stunRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                typeof(Fighter).GetField("_knockbackVelocity", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(player, Vector3.zero);
                UpdatePlayer(player);
            }
            // 共通使用キーを実際の戦闘更新まで通し、直接発動と同じ回数管理を確認します。
            player.TakeDamage(10, player.Facing);
            SetPlayerTimer(player, "_stunRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(player.Health == player.MaxHealth && spellCaster.HealingUsesRemaining == healingBeforeSelection - 1,
                "E入力で選択した回復を実際に発動し、一度だけ消費");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(spellCaster.HealingUsesRemaining == healingBeforeSelection - 1, "共通使用キーの保持で連続消費しない");
            // 後続の戦闘検証には開始時の使用回数と硬直を引き継ぎます。
            typeof(ClericFighter).GetProperty("HealingUsesRemaining").SetValue(spellCaster, healingBeforeSelection);
            typeof(ClericFighter).GetField("_recoveryRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spellCaster, 0f);
            typeof(Fighter).GetField("_knockbackVelocity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, Vector3.zero);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            // 入力検証の範囲魔法で、後続の戦闘検証用の敵にダメージを残さないようにします。
            Fighter[] turnInputEnemies = UnityEngine.Object.FindObjectsByType<Fighter>();
            var turnInputPositions = new System.Collections.Generic.Dictionary<Fighter, Vector3>();
            foreach (Fighter turnInputEnemy in turnInputEnemies)
            {
                if (!turnInputEnemy.IsPlayer)
                {
                    turnInputPositions.Add(turnInputEnemy, turnInputEnemy.transform.position);
                    turnInputEnemy.transform.position = player.transform.position + Vector3.right * (BrawlerBalance.TurnUndeadRadius + 2);
                }
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J, Key.U));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(spellCaster.IsTurningUndead && !player.IsAirborne,
                "攻撃＋ジャンプ同時押しは通常攻撃・ジャンプより優先して発動");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J, Key.U));
            InputSystem.Update();
            Require(!BrawlerInput.WasTurnUndeadPressed(), "両ボタン保持では発動を繰り返さない");
            foreach (var turnInputPosition in turnInputPositions)
            {
                turnInputPosition.Key.transform.position = turnInputPosition.Value;
            }
            typeof(ClericFighter).GetField("_turnUndeadRecoveryRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spellCaster, 0f);
            typeof(ClericFighter).GetField("_turnUndeadCooldownRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spellCaster, 0f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            ValidateComboAndCancelInput(player, pad);
            ValidateBackstepInput(player, keyboard);
            ValidateGuardReleaseInput(player);
            ValidateFighterClassInput(player, keyboard);
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

    private static void ValidateFighterClassInput(Fighter sourcePlayer, Keyboard keyboard)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var player = (SwordFighter)factory.CreateFighter("Test sword player", Vector3.zero, true, 1,
            playerClass: BrawlerDefines.PlayerClass.Fighter);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            Require(player.ClassSettings == PlayerClassCatalog.Fighter && Array.Exists(player.GetComponentsInChildren<Transform>(), part => part.name == "Sword"),
                "ファイターの派生クラス・職業設定・剣を生成");
            Require(player.Inventory.GetAction(0) == BrawlerDefines.UsableAction.None
                && player.CanUseAction(BrawlerDefines.UsableAction.ThrowingKnife)
                && !player.CanUseAction(BrawlerDefines.UsableAction.Healing), "ファイターは初期空き枠で、ナイフ可・魔法不可");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F2));
            InputSystem.Update();
            Require(BrawlerInput.WasFighterSelected() && !BrawlerInput.WasClericSelected(), "F2でファイター開始を選択");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
            InputSystem.Update();
            Require(BrawlerInput.WasClericSelected(), "F1でクレリック開始を選択");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            SendState(pad, new GamepadState().WithButton(GamepadButton.DpadRight));
            Require(!BrawlerInput.WasFighterSelected(), "通常の右移動では職業を変更しない");
            SendState(pad, new GamepadState());
            SendState(pad, new GamepadState().WithButton(GamepadButton.Select).WithButton(GamepadButton.DpadRight));
            Require(BrawlerInput.WasFighterSelected(), "View・Shareと右でファイター選択");
            SendState(pad, new GamepadState().WithButton(GamepadButton.Select).WithButton(GamepadButton.DpadLeft));
            Require(BrawlerInput.WasClericSelected(), "View・Shareと左でクレリック選択");
            SendState(pad, new GamepadState());
            Require(player.TryStoreThrowingKnife(), "ファイターがナイフを所持欄へ保存");
            SendState(pad, new GamepadState { rightTrigger = 1 });
            read.Invoke(player, null);
            Require(player.ThrowingKnivesHeld == 0 && player.SelectedAction == BrawlerDefines.UsableAction.None,
                "パッド使用入力でナイフを消費し、0なら同じ枠が空になる");
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            read.Invoke(player, null);
            Require(ReadPlayerTimer(player, "_attackBufferRemaining") == 0, "ファイターも使用後硬直中は攻撃しない");
            SetPlayerTimer(player, "_itemRecoveryRemaining", 0);
            Require(player.TryStoreThrowingHammer() && player.TryUseSelectedAction()
                && player.ThrowingHammersHeld == 0, "ファイターもハンマーを使用可能");
            SetPlayerTimer(player, "_itemRecoveryRemaining", 0);
            Require(player.TryStoreHealingPotion() && !player.TryUseSelectedAction(), "HP満タンではポーションを消費しない");
            player.TakeDamage(1, player.Facing);
            SetPlayerTimer(player, "_stunRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            Require(player.TryUseSelectedAction() && player.Health == player.MaxHealth
                && player.HealingPotionsHeld == 0, "ポーション使用で回復し一度だけ消費");
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
            // この項目で発射した投擲物だけを除去し、後続の軌道検証へ残しません。
            foreach (PlayerThrownProjectile projectile in UnityEngine.Object.FindObjectsByType<PlayerThrownProjectile>())
            {
                if ((Fighter)typeof(PlayerThrownProjectile).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(projectile) == player)
                {
                    UnityEngine.Object.DestroyImmediate(projectile.gameObject);
                }
            }
            UnityEngine.Object.DestroyImmediate(player.gameObject);
        }
    }

    private static void ValidateGuardReleaseInput(Fighter sourcePlayer)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo timers = typeof(Fighter).GetMethod("UpdateTimers", BindingFlags.Instance | BindingFlags.NonPublic);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            foreach (float facing in new[] { -1f, 1f })
            {
                Fighter player = factory.CreateFighter("Test guard release", Vector3.zero, true, 1);
                try
                {
                    typeof(Fighter).GetProperty("Facing").SetValue(player, facing);
                    SendState(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
                    read.Invoke(player, null);
                    Require(player.IsGuarding, "専用パッドボタンでガード開始");
                    SendState(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder)
                        .WithButton(facing < 0 ? GamepadButton.DpadLeft : GamepadButton.DpadRight));
                    read.Invoke(player, null);
                    Require(player.IsUppercutAttacking && !player.IsGuarding && player.Facing == facing,
                        "パッドの前入力だけで左右の対空反撃へ移行");
                    int health = player.Health;
                    player.TakeDamage(1, facing);
                    Require(player.Health == health, "ガード解除直後は背面からの攻撃も無敵");
                    timers.Invoke(player, new object[] { 0f });
                    Require(ReadPlayerTimer(player, "_guardReleaseRemaining") > 0, "停止時間では解除後の無敵が減らない");
                    timers.Invoke(player, new object[] { BrawlerBalance.GuardReleaseInvulnerabilitySeconds });
                    player.TakeDamage(1, facing);
                    Require(player.Health == health - 1, "解除後の無敵が終了すると攻撃中でも被弾");
                    SetPlayerTimer(player, "_stunRemaining", 0);
                    SetPlayerTimer(player, "_attackCooldown", 0);
                    SendState(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
                    read.Invoke(player, null);
                    Require(player.IsGuarding, "解除のみの検証に向けて再度ガード");
                    SendState(pad, new GamepadState());
                    read.Invoke(player, null);
                    Require(!player.IsGuarding && ReadPlayerTimer(player, "_guardReleaseRemaining") > 0,
                        "ガードボタンを離した場合も保護時間が始まる");
                    timers.Invoke(player, new object[] { BrawlerBalance.GuardReleaseInvulnerabilitySeconds });
                    read.Invoke(player, null);
                    Require(ReadPlayerTimer(player, "_guardReleaseRemaining") == 0,
                        "待機入力で解除後の無敵を繰り返し延長しない");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(player.gameObject);
                    SendState(pad, new GamepadState());
                }
            }
        }
        finally
        {
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
        }
    }

    private static void ValidateBackstepInput(Fighter sourcePlayer, Keyboard keyboard)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            // 独立した履歴でも、停止中の順番入力と受付時間の境界を維持します。
            var buttons = new ButtonSequenceBuffer();
            float window = BrawlerBalance.BackstepInputWindowSeconds;
            Require(!buttons.Record(false, true, 0f, window), "先行入力なしでは順番入力が成立しない");
            Require(!buttons.Record(true, true, 0f, window), "同時押しは順番入力にしない");
            Require(buttons.Record(false, true, 0f, window), "同じゲーム時刻でも次の入力更新なら順番入力が成立");
            Require(buttons.Record(false, true, window, window), "受付時間ちょうどの入力は受理");
            Require(!buttons.Record(false, true, window * 2, window), "受付時間を過ぎると拒否");
            buttons.Reset();
            Require(!buttons.Record(false, true, 0f, window), "消費後の履歴では再発動しない");
            // 毎回新しいキャラクターを作り、直前の回避・魔法の履歴を混ぜません。
            foreach (Key firstButton in new[] { Key.J, Key.Q, Key.E })
            {
                Fighter player = factory.CreateFighter("Test keyboard backstep", Vector3.zero, true, 1);
                try
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(firstButton));
                    InputSystem.Update();
                    read.Invoke(player, null);
                    // 使用成功時の硬直は後述の専用ケースで確認します。
                    SetPlayerTimer(player, "_itemRecoveryRemaining", 0);
                    typeof(ClericFighter).GetField("_recoveryRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 0f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                    read.Invoke(player, null);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U));
                    InputSystem.Update();
                    read.Invoke(player, null);
                    Require(player.IsBackstepping && !player.IsAirborne, $"キーボードの{firstButton}→ジャンプで後方回避");
                    Require(ReadPlayerTimer(player, "_attackRemaining") == 0, "中断した攻撃の判定を残さない");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(player.gameObject);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                }
            }
            foreach (GamepadState firstState in new[]
            {
                new GamepadState().WithButton(GamepadButton.West),
                new GamepadState().WithButton(GamepadButton.Select),
                new GamepadState { rightTrigger = 1 }
            })
            {
                Fighter player = factory.CreateFighter("Test pad backstep", Vector3.zero, true, 1);
                try
                {
                    SendState(pad, firstState);
                    read.Invoke(player, null);
                    SetPlayerTimer(player, "_itemRecoveryRemaining", 0);
                    typeof(ClericFighter).GetField("_recoveryRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 0f);
                    SendState(pad, new GamepadState());
                    read.Invoke(player, null);
                    SendState(pad, new GamepadState().WithButton(GamepadButton.South));
                    read.Invoke(player, null);
                    Require(player.IsBackstepping, "パッドの攻撃・選択・使用→ジャンプで後方回避");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(player.gameObject);
                    SendState(pad, new GamepadState());
                }
            }
            Fighter blocked = factory.CreateFighter("Test blocked backstep", Vector3.zero, true, 1);
            try
            {
                GetBackstepButtons(blocked).Record(true, false, Time.time - BrawlerBalance.BackstepInputWindowSeconds - 1f, BrawlerBalance.BackstepInputWindowSeconds);
                SendState(pad, new GamepadState().WithButton(GamepadButton.South));
                read.Invoke(blocked, null);
                Require(!blocked.IsBackstepping && blocked.IsAirborne, "受付時間を過ぎた入力は通常ジャンプ");
                SetPlayerTimer(blocked, "_jumpHeight", 0);
                SetPlayerTimer(blocked, "_jumpVelocity", 0);
                GetBackstepButtons(blocked).Record(true, false, Time.time, BrawlerBalance.BackstepInputWindowSeconds);
                SetPlayerTimer(blocked, "_itemRecoveryRemaining", BrawlerBalance.BackstepInputWindowSeconds);
                SendState(pad, new GamepadState());
                SendState(pad, new GamepadState().WithButton(GamepadButton.South));
                read.Invoke(blocked, null);
                Require(!blocked.IsBackstepping, "アイテム使用後の硬直を後方回避で省略しない");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(blocked.gameObject);
            }
        }
        finally
        {
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
        }
    }

    private static void ValidateComboAndCancelInput(Fighter sourcePlayer, Gamepad pad)
    {
        // 接続解除の検証後なので、この項目専用の仮想パッドを接続します。
        bool ownsPad = !pad.added;
        if (ownsPad)
        {
            pad = InputSystem.AddDevice<Gamepad>();
        }
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        Fighter player = factory.CreateFighter("Test combo input", Vector3.zero, true, 1);
        MethodInfo attack = typeof(Fighter).GetMethod("Attack", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo movement = typeof(Fighter).GetMethod("GetMovement", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            typeof(Fighter).GetProperty("Facing").SetValue(player, 1f);
            SendState(pad, new GamepadState());
            attack.Invoke(player, null);
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            read.Invoke(player, null);
            Require(ReadPlayerTimer(player, "_attackBufferRemaining") == 0, "パッドの命中前の連打は予約しない");
            // 実ダメージによる通知はCombatValidationで確認し、ここでは入力後の分岐を確認します。
            SendState(pad, new GamepadState());
            player.NotifyMeleeHit();
            read.Invoke(player, null);
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            read.Invoke(player, null);
            Require(ReadPlayerTimer(player, "_attackBufferRemaining") > 0, "命中後のパッド入力は次段を予約する");
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            movement.Invoke(player, new object[] { Vector3.zero, false });
            Require(player.ComboStep == 2, "パッドの命中後入力から2段目へ進む");
            typeof(Fighter).GetField("_hasDealtHit", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, true);
            SendState(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
            read.Invoke(player, null);
            SendState(pad, new GamepadState());
            read.Invoke(player, null);
            SendState(pad, new GamepadState().WithButton(GamepadButton.DpadUp));
            read.Invoke(player, null);
            SendState(pad, new GamepadState().WithButton(GamepadButton.DpadUp).WithButton(GamepadButton.West));
            read.Invoke(player, null);
            Require(player.IsUppercutAttacking, "パッドの下→上＋攻撃で通常攻撃の途中から対空へキャンセルする");
            typeof(Fighter).GetField("_hasDealtHit", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, true);
            SendState(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
            read.Invoke(player, null);
            SendState(pad, new GamepadState().WithButton(GamepadButton.DpadDown).WithButton(GamepadButton.DpadRight).WithButton(GamepadButton.West));
            read.Invoke(player, null);
            Require(player.IsUppercutAttacking && !player.IsDashSpecialAttacking,
                "パッドでも対空からダッシュ必殺技への逆方向キャンセルは成立しない");
            SendState(pad, new GamepadState());
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            attack.Invoke(player, null);
            SendState(pad, new GamepadState().WithButton(GamepadButton.West));
            player.NotifyMeleeHit();
            Require(player.IsPushAttacking, "パッドの攻撃保持中の実命中通知で押し攻撃へ移行");
            float pushRemaining = ReadPlayerTimer(player, "_attackRemaining");
            player.NotifyMeleeHit();
            Require(ReadPlayerTimer(player, "_attackRemaining") == pushRemaining,
                "押し攻撃の命中で保持を再発動しない");
        }
        finally
        {
            SendState(pad, new GamepadState());
            if (ownsPad)
            {
                InputSystem.RemoveDevice(pad);
            }
            UnityEngine.Object.DestroyImmediate(player.gameObject);
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

    private static ButtonSequenceBuffer GetBackstepButtons(Fighter player)
    {
        return (ButtonSequenceBuffer)typeof(Fighter).GetField("_backstepButtons", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(player);
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
