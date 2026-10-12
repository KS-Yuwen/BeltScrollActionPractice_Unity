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
            Require(player.Health == player.MaxHealth - 10 && spellCaster.IsCastingSpell
                && spellCaster.HealingUsesRemaining == healingBeforeSelection - 1,
                "E入力で回復の詠唱を開始し、一度だけ消費する");
            AdvanceSpell(spellCaster, BrawlerBalance.HealingCastSeconds);
            Require(player.Health == player.MaxHealth && spellCaster.IsReleasingSpell,
                "回復は詠唱完了時に発動する");
            AdvanceSpell(spellCaster, BrawlerBalance.HealingEffectSeconds);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            UpdatePlayer(player);
            Require(spellCaster.HealingUsesRemaining == healingBeforeSelection - 1, "共通使用キーの保持で連続消費しない");
            // 後続の戦闘検証には開始時の使用回数と硬直を引き継ぎます。
            typeof(ClericFighter).GetProperty("HealingUsesRemaining").SetValue(spellCaster, healingBeforeSelection);
            typeof(ClericFighter).GetField("_recoveryRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spellCaster, 0f);
            typeof(SpellFighter).GetField("_spellRecoveryRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spellCaster, 0f);
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
            ValidateDwarfClassInput(player, keyboard);
            ValidateThiefClassInput(player, keyboard);
            ValidateElfClassInput(player, keyboard);
            ValidateSupportSpellInput();
            ValidateHealingSpellInput();
            ValidateAttackSpellInput(player, keyboard);
            ValidateThiefAirJumpInput(player, keyboard);
            ValidateThiefWallJumpInput(player);
            ValidateBackstabInput(player);
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

    private static void ValidateBackstabInput(Fighter sourcePlayer)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var enemies = (System.Collections.Generic.List<Fighter>)typeof(BeltBrawler).GetField("_enemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        Fighter[] previousEnemies = enemies.ToArray();
        Fighter player = factory.CreateFighter("Test backstab", Vector3.zero, true, 1, playerClass: BrawlerDefines.PlayerClass.Thief);
        Fighter target = factory.CreateFighter("Test backstab target", Vector3.right, false, 1, isHeavy: true);
        Fighter boss = factory.CreateFighter("Test backstab boss", Vector3.right, false, 1, isBoss: true);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo animate = typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo canTarget = typeof(BeltBrawler).GetMethod("CanBackstabTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            enemies.Clear();
            enemies.Add(target);
            Require(!boss.CanBeBackstabbed, "ボスはバックスタブ対象外");
            foreach (float facing in new[] { -1f, 1f })
            {
                SetPlayerTimer(player, "_attackRemaining", 0);
                SetPlayerTimer(player, "_attackCooldown", 0);
                SetPlayerTimer(target, "_stunRemaining", 0);
                SetPlayerTimer(target, "_downRemaining", 0);
                typeof(Fighter).GetProperty("Facing").SetValue(player, facing);
                typeof(Fighter).GetProperty("Facing").SetValue(target, facing);
                target.transform.position = Vector3.right * facing * BrawlerBalance.BackstabReach * 0.7f;
                SendState(pad, new GamepadState());
                read.Invoke(player, null);
                GamepadButton backward = facing > 0 ? GamepadButton.DpadLeft : GamepadButton.DpadRight;
                GamepadButton forward = facing > 0 ? GamepadButton.DpadRight : GamepadButton.DpadLeft;
                SendState(pad, new GamepadState().WithButton(backward));
                read.Invoke(player, null);
                // 後ろへの移動で向きが変わっても、コマンド開始時の前方向を記憶します。
                typeof(Fighter).GetProperty("Facing").SetValue(player, -facing);
                SendState(pad, new GamepadState().WithButton(forward));
                read.Invoke(player, null);
                SendState(pad, new GamepadState().WithButton(forward).WithButton(GamepadButton.West));
                read.Invoke(player, null);
                Require(player.IsBackstabbing && player.Facing == facing, "左右とも後ろ→前→攻撃でバックスタブを優先");
                int health = target.Health;
                animate.Invoke(player, new object[] { BrawlerBalance.BackstabHitDelaySeconds * 1.1f });
                Require(target.Health == health - BrawlerBalance.BackstabDamage, "選択した背後の敵へ専用威力で命中");
                animate.Invoke(player, new object[] { 0.01f });
                Require(target.Health == health - BrawlerBalance.BackstabDamage, "同じバックスタブで二重命中しない");
                SetPlayerTimer(target, "_downRemaining", 0);
                typeof(Fighter).GetProperty("Facing").SetValue(target, -facing);
                Require(!(bool)canTarget.Invoke(game, new object[] { player, target, facing }), "正面からはバックスタブ対象にしない");
                int turnedHealth = target.Health;
                game.HitBackstabTarget(player, target, BrawlerBalance.BackstabDamage);
                Require(target.Health == turnedHealth, "命中前に振り向いた敵にはバックスタブを当てない");
            }
            var command = new BackstabCommandBuffer();
            command.Record(Vector3.left, 1f, 0f);
            command.Record(Vector3.right, -1f, 0.1f);
            Require(!command.TryConsume(Vector3.right, 0.1f, out _), "前と攻撃の同時入力では成立しない");
            command.Record(Vector3.right, 1f, 0.2f);
            Require(command.TryConsume(Vector3.right, 0.2f, out _) && !command.TryConsume(Vector3.right, 0.2f, out _),
                "先行方向入力後だけ受理し、一度だけ消費");
        }
        finally
        {
            enemies.Clear();
            enemies.AddRange(previousEnemies);
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
            UnityEngine.Object.DestroyImmediate(player.gameObject);
            UnityEngine.Object.DestroyImmediate(target.gameObject);
            UnityEngine.Object.DestroyImmediate(boss.gameObject);
        }
    }

    private static void ValidateThiefWallJumpInput(Fighter sourcePlayer)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        Fighter player = factory.CreateFighter("Test thief wall jump", Vector3.zero, true, 1,
            playerClass: BrawlerDefines.PlayerClass.Thief);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        MethodInfo jump = typeof(Fighter).GetMethod("StartJump", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo wallJump = typeof(Fighter).GetMethod("TryStartWallJump", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo move = typeof(Fighter).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            foreach (float side in new[] { -1f, 1f })
            {
                SendState(pad, new GamepadState());
                player.transform.position = Vector3.right * side * BrawlerBalance.StageHalfWidth;
                jump.Invoke(player, new object[] { Vector3.zero });
                SetPlayerTimer(player, "_lastJumpPressedAt", BrawlerDefines.UnsetTime);
                SetPlayerTimer(player, "_jumpHeight", 1f);
                SendState(pad, new GamepadState().WithButton(GamepadButton.South));
                read.Invoke(player, null);
                Vector3 drift = (Vector3)typeof(Fighter).GetField("_jumpDrift", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                Require(player.Facing == -side && drift.x == -side * BrawlerBalance.ThiefWallJumpDriftSpeed
                    && ReadPlayerTimer(player, "_jumpVelocity") == BrawlerBalance.ThiefWallJumpVelocity,
                    "左右の壁から内側へ向いて三角跳び");
                Require(player.AirJumpsRemaining == BrawlerBalance.ThiefExtraAirJumps,
                    "壁際では三角跳びを優先し、2段ジャンプの回数は残す");
                float before = player.transform.position.x;
                move.Invoke(player, new object[] { Vector3.zero, 0.1f });
                Require((player.transform.position.x - before) * side < 0,
                    "見た目だけでなく実移動でステージの内側へ跳ね返る");
                Require(!(bool)wallJump.Invoke(player, null), "空中で三角跳びを繰り返さない");
                SendState(pad, new GamepadState());
                SendState(pad, new GamepadState().WithButton(GamepadButton.South));
                read.Invoke(player, null);
                Require(player.AirJumpsRemaining == 0, "三角跳び後に残した2段ジャンプを使用できる");
                typeof(Fighter).GetMethod("UpdateJump", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, new object[] { 2f });
                Require(!player.IsAirborne, "三角跳び後も着地する");
            }
            jump.Invoke(player, new object[] { Vector3.zero });
            player.transform.position = Vector3.forward * BrawlerBalance.StageHalfDepth;
            Require(!(bool)wallJump.Invoke(player, null), "中央・奥行きの端では三角跳びしない");
            player.transform.position = Vector3.right * BrawlerBalance.StageHalfWidth;
            SetPlayerTimer(player, "_attackRemaining", BrawlerBalance.AttackSeconds);
            Require(!(bool)wallJump.Invoke(player, null), "空中攻撃を三角跳びで中断しない");
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_stunRemaining", BrawlerBalance.StunSeconds);
            Require(!(bool)wallJump.Invoke(player, null), "被弾硬直中は三角跳びしない");
            SetPlayerTimer(player, "_stunRemaining", 0);
            Require((bool)wallJump.Invoke(player, null), "着地後のジャンプでは三角跳びの回数が戻る");
        }
        finally
        {
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
            UnityEngine.Object.DestroyImmediate(player.gameObject);
        }
    }

    private static void ValidateThiefAirJumpInput(Fighter sourcePlayer, Keyboard keyboard)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        Fighter player = factory.CreateFighter("Test thief air jump", Vector3.zero, true, 1,
            playerClass: BrawlerDefines.PlayerClass.Thief);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        MethodInfo jump = typeof(Fighter).GetMethod("StartJump", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo updateJump = typeof(Fighter).GetMethod("UpdateJump", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            jump.Invoke(player, new object[] { Vector3.zero });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U));
            InputSystem.Update();
            read.Invoke(player, null);
            Require(player.IsBackstepping && player.AirJumpsRemaining == 0, "シーフでも素早い2回押しはバックステップを優先");
            SetPlayerTimer(player, "_dashRemaining", 0);
            SetPlayerTimer(player, "_dashCooldown", 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            jump.Invoke(player, new object[] { Vector3.zero });
            SetPlayerTimer(player, "_lastJumpPressedAt", Time.time - BrawlerBalance.BackstepInputWindowSeconds * 2);
            SetPlayerTimer(player, "_jumpHeight", 1f);
            SetPlayerTimer(player, "_jumpVelocity", -1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U));
            InputSystem.Update();
            read.Invoke(player, null);
            Require(player.IsAirborne && !player.IsBackstepping && player.AirJumpsRemaining == 0
                && ReadPlayerTimer(player, "_jumpVelocity") == BrawlerBalance.ThiefAirJumpVelocity
                && ReadPlayerTimer(player, "_jumpHeight") == 1f, "キーボードの2段ジャンプは現在高度から再上昇し一度だけ消費");
            SendState(pad, new GamepadState().WithButton(GamepadButton.South));
            read.Invoke(player, null);
            Require(player.AirJumpsRemaining == 0, "デバイスを変えても3段目を発動しない");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            SendState(pad, new GamepadState());
            updateJump.Invoke(player, new object[] { 2f });
            Require(!player.IsAirborne, "2段ジャンプ後も床へ着地する");
            jump.Invoke(player, new object[] { Vector3.zero });
            SetPlayerTimer(player, "_lastJumpPressedAt", BrawlerDefines.UnsetTime);
            SendState(pad, new GamepadState().WithButton(GamepadButton.South));
            read.Invoke(player, null);
            Require(player.AirJumpsRemaining == 0 && !player.IsBackstepping, "着地後に回数を戻し、パッドでも2段ジャンプを発動");
            SendState(pad, new GamepadState());
            updateJump.Invoke(player, new object[] { 2f });
            jump.Invoke(player, new object[] { Vector3.zero });
            SetPlayerTimer(player, "_lastJumpPressedAt", BrawlerDefines.UnsetTime);
            SetPlayerTimer(player, "_attackRemaining", BrawlerBalance.AttackSeconds);
            SendState(pad, new GamepadState().WithButton(GamepadButton.South));
            read.Invoke(player, null);
            Require(player.AirJumpsRemaining == BrawlerBalance.ThiefExtraAirJumps,
                "空中攻撃中は追加ジャンプで攻撃を中断せず、回数も消費しない");
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
            UnityEngine.Object.DestroyImmediate(player.gameObject);
        }
    }

    // 実入力から発動し、対象制限・硬直・独立した回数管理まで確認します。
    private static void ValidateAttackSpellInput(Fighter sourcePlayer, Keyboard keyboard)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var enemies = (System.Collections.Generic.List<Fighter>)typeof(BeltBrawler).GetField("_enemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        Fighter[] previous = enemies.ToArray();
        var player = (ElfFighter)factory.CreateFighter("Test spell", Vector3.zero, true, 1, playerClass: BrawlerDefines.PlayerClass.Elf);
        Fighter target = factory.CreateFighter("Test spell target", Vector3.right, false, 1, isHeavy: true);
        Fighter boss = factory.CreateFighter("Test paused boss", Vector3.forward * 2f, false, 1, isBoss: true);
        FieldInfo bossRecovery = typeof(BossFighter).GetField("_recoveryRemaining", BindingFlags.Instance | BindingFlags.NonPublic);
        var projectileObject = new GameObject("Test paused enemy projectile");
        var projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Initialize(player, 1, game);
        projectile.transform.position = Vector3.right * 10f;
        MethodInfo updateProjectile = typeof(EnemyProjectile).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo lifetime = typeof(EnemyProjectile).GetField("_remainingLifetime", BindingFlags.Instance | BindingFlags.NonPublic);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        FieldInfo recovery = typeof(SpellFighter).GetField("_spellRecoveryRemaining", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo tick = typeof(SpellFighter).GetMethod("UpdateSpell", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo animate = typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic);
        Transform arm = (Transform)typeof(Fighter).GetField("_attackArm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
        Action<float, string> requireArm = (angle, message) =>
        {
            // 表示用の角度だけでなく、実際の腕Transformへ反映された姿勢を調べます。
            animate.Invoke(player, new object[] { 0f });
            Require(Quaternion.Angle(arm.localRotation, Quaternion.Euler(0, 0, angle)) < 0.01f, message);
        };
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            enemies.Clear();
            enemies.Add(target);
            Require(player.Inventory.GetCount(BrawlerDefines.UsableAction.MagicMissile) == BrawlerBalance.MagicMissileUses,
                "エルフの魔法は初期3回で選択済み");
            foreach (float facing in new[] { -1f, 1f })
            {
                recovery.SetValue(player, 0f);
                SetPlayerTimer(target, "_stunRemaining", 0);
                SetPlayerTimer(target, "_downRemaining", 0);
                typeof(Fighter).GetProperty("Facing").SetValue(player, facing);
                target.transform.position = Vector3.right * facing * 2f;
                int before = target.Health;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                SendState(pad, new GamepadState());
                if (facing < 0)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    InputSystem.Update();
                }
                else
                {
                    SendState(pad, new GamepadState { rightTrigger = 1f });
                }
                read.Invoke(player, null);
                Require(player.IsCastingSpell && !player.IsRecoveringFromSpell && target.Health == before,
                    "入力直後は詠唱だけ開始し、効果と後隙はまだ発生しない");
                requireArm(BrawlerBalance.SpellReadyArmAngle, "左右とも詠唱開始時は通常姿勢から構え始める");
                int castCount = player.Inventory.GetCount(BrawlerDefines.UsableAction.MagicMissile);
                player.SelectNextAction();
                Require(!player.TryUseSelectedAction(), "詠唱中は別の所持品を使用できない");
                for (int slot = 1; slot < player.Inventory.SlotCount; slot++) player.SelectNextAction();
                tick.Invoke(player, new object[] { 0f });
                Require(player.SpellCastSecondsRemaining == BrawlerBalance.MagicMissileCastSeconds,
                    "ヒットストップ中は詠唱時間を減らさない");
                tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileCastSeconds / 2 });
                Require(target.Health == before && player.IsCastingSpell
                    && player.Inventory.GetCount(BrawlerDefines.UsableAction.MagicMissile) == castCount,
                    "詠唱途中にはダメージ・追加消費がない");
                requireArm(BrawlerBalance.SpellCastingArmAngle, "詠唱途中は腕を持ち上げた構えを維持");
                tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileCastSeconds / 2 - BrawlerBalance.SpellReleaseArmSeconds / 2 });
                requireArm(Mathf.Lerp(BrawlerBalance.SpellCastingArmAngle, BrawlerBalance.SpellReleaseArmAngle, 0.5f),
                    "詠唱の最後に腕を前へ振り出し、まだ効果は発動しない");
                Require(target.Health == before, "腕を振り出す途中に早く命中させない");
                tick.Invoke(player, new object[] { player.SpellCastSecondsRemaining });
                requireArm(BrawlerBalance.SpellReleaseArmAngle, "効果発動の瞬間は腕を前へ伸ばした姿勢");
                Require(game.IsSpellEffectActive && player.IsReleasingSpell && !player.IsRecoveringFromSpell,
                    "詠唱完了で敵停止を開始し、後隙はまだ始めない");
                float stun = ReadPlayerTimer(target, "_stunRemaining");
                Vector3 enemyPosition = target.transform.position;
                UpdatePlayer(target);
                Require(target.transform.position == enemyPosition && ReadPlayerTimer(target, "_stunRemaining") == stun,
                    "魔法効果中は敵の移動・硬直の経過を止める");
                bossRecovery.SetValue(boss, BrawlerBalance.BossRecoverySeconds);
                UpdatePlayer(boss);
                Require((float)bossRecovery.GetValue(boss) == BrawlerBalance.BossRecoverySeconds, "ボス固有の後隙も停止");
                float remainingLifetime = (float)lifetime.GetValue(projectile);
                Vector3 bulletPosition = projectile.transform.position;
                updateProjectile.Invoke(projectile, null);
                Require(projectile.transform.position == bulletPosition && (float)lifetime.GetValue(projectile) == remainingLifetime,
                    "発射済みの敵の弾も位置と寿命を維持");
                Require(Time.timeScale > 0, "魔法命中では全体ヒットストップを起こさず、演出の時間を進める");
                tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileEffectSeconds / 2 });
                Require(game.IsSpellEffectActive && player.SpellEffectProgress > 0 && !player.IsRecoveringFromSpell,
                    "敵停止中にも魔法演出だけは進行");
                requireArm(BrawlerBalance.SpellReleaseArmAngle, "魔法演出中は放つ姿勢を維持");
                tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileEffectSeconds / 2 });
                Require(!game.IsSpellEffectActive && !player.IsReleasingSpell, "演出終了時に敵停止を解除");
                Require(target.Health == before - BrawlerBalance.MagicMissileDamage && player.IsRecoveringFromSpell,
                    "キーボード・パッドから左右へ専用威力で魔法を発動");
                int count = player.Inventory.GetCount(BrawlerDefines.UsableAction.MagicMissile);
                Require(!player.TryUseSelectedAction() && player.Inventory.GetCount(BrawlerDefines.UsableAction.MagicMissile) == count,
                    "硬直中は連続使用して消費しない");
                tick.Invoke(player, new object[] { BrawlerBalance.SpellReleasePoseHoldSeconds });
                requireArm(BrawlerBalance.SpellReleaseArmAngle, "発動直後は腕を伸ばした姿勢を少し維持");
                tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileRecoverySeconds - BrawlerBalance.SpellReleasePoseHoldSeconds });
                requireArm(BrawlerBalance.SpellReadyArmAngle, "後隙終了時には腕を通常姿勢へ戻す");
                Require(!player.IsCastingSpell && !player.IsRecoveringFromSpell
                    && target.Health == before - BrawlerBalance.MagicMissileDamage, "後隙終了時は再行動可能で二重発動しない");
            }
            recovery.SetValue(player, 0f);
            SetPlayerTimer(player, "_jumpHeight", 1f);
            Require(!player.TryUseSelectedAction(), "空中では魔法を消費しない");
            SetPlayerTimer(player, "_jumpHeight", 0f);
            target.transform.position = Vector3.left;
            int health = target.Health;
            Require(player.TryUseSelectedAction() && target.Health == health
                && player.SelectedAction == BrawlerDefines.UsableAction.None, "後方には命中せず、最後の1回でNOTHINGになる");
            Require(player.IsCastingSpell, "最後の1回で空き枠になっても予約した魔法の詠唱は維持");
            tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileCastSeconds });
            tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileEffectSeconds });
            Require(!player.IsCastingSpell && player.IsRecoveringFromSpell && target.Health == health,
                "最後の1回でも詠唱完了後に発動し、後方には命中しない");
            Require(player.TryRestoreSpellUses(BrawlerDefines.UsableAction.MagicMissile, BrawlerBalance.InventoryStackLimit)
                && !player.TryRestoreSpellUses(BrawlerDefines.UsableAction.MagicMissile, 1), "空き枠へ補充し上限9を守る");
            recovery.SetValue(player, 0f);
            foreach (Vector3 position in new[] { Vector3.right * (BrawlerBalance.MagicMissileRange + 1f),
                Vector3.right + Vector3.forward * (BrawlerBalance.MagicMissileDepth + 1f) })
            {
                target.transform.position = position;
                Require(player.TryUseSelectedAction(), "射程外の敵しかいなくても詠唱を開始できる");
                tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileCastSeconds });
            tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileEffectSeconds });
                Require(target.Health == health, "射程・奥行きの範囲外には命中しない");
                recovery.SetValue(player, 0f);
            }
            target.transform.position = Vector3.right;
            Require(player.TryUseSelectedAction(), "中断検証用の詠唱を開始");
            int consumedCount = player.Inventory.GetCount(BrawlerDefines.UsableAction.MagicMissile);
            player.TakeDamage(1, -1);
            Require(!player.IsCastingSpell, "実際の被弾で詠唱を直ちに中断");
            requireArm(BrawlerBalance.SpellReadyArmAngle, "被弾中断後に詠唱の腕の構えを残さない");
            tick.Invoke(player, new object[] { BrawlerBalance.MagicMissileCastSeconds + BrawlerBalance.MagicMissileRecoverySeconds });
            Require(target.Health == health && player.Inventory.GetCount(BrawlerDefines.UsableAction.MagicMissile) == consumedCount,
                "中断後は遅れて発動せず、消費済みの回数も返却しない");
        }
        finally
        {
            enemies.Clear();
            enemies.AddRange(previous);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
            UnityEngine.Object.DestroyImmediate(projectileObject);
            UnityEngine.Object.DestroyImmediate(boss.gameObject);
            UnityEngine.Object.DestroyImmediate(player.gameObject);
            UnityEngine.Object.DestroyImmediate(target.gameObject);
            foreach (LineRenderer line in UnityEngine.Object.FindObjectsByType<LineRenderer>())
            {
                if (line.name == "Magic Missile Trail") UnityEngine.Object.DestroyImmediate(line.gameObject);
            }
        }
    }

    private static void ValidateElfClassInput(Fighter sourcePlayer, Keyboard keyboard)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var player = (ElfFighter)factory.CreateFighter("Test elf player", Vector3.zero, true, 1,
            playerClass: BrawlerDefines.PlayerClass.Elf);
        Fighter target = factory.CreateFighter("Test elf reach", Vector3.right, false, 1);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            Require(player.ClassSettings == PlayerClassCatalog.Elf
                && Array.Exists(player.GetComponentsInChildren<Transform>(), part => part.name == "Sword")
                && Array.Exists(player.GetComponentsInChildren<Transform>(), part => part.name == "Left elf ear"),
                "エルフの派生クラス・職業設定・剣を生成");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F5));
            InputSystem.Update();
            Require(BrawlerInput.WasElfSelected(), "F5でエルフを選択");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            SendState(pad, new GamepadState().WithButton(GamepadButton.North));
            Require(!BrawlerInput.WasElfSelected(), "通常のY・△入力では職業を変えない");
            SendState(pad, new GamepadState());
            SendState(pad, new GamepadState().WithButton(GamepadButton.Select).WithButton(GamepadButton.North));
            Require(BrawlerInput.WasElfSelected(), "View・ShareとY・△でエルフを選択");
            SendState(pad, new GamepadState());
            MethodInfo move = typeof(Fighter).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic);
            move.Invoke(player, new object[] { Vector3.right, 0.1f });
            Require(Mathf.Approximately(player.transform.position.x, BrawlerBalance.ElfWalkSpeed * 0.1f),
                "実移動にエルフの歩行速度を適用");
            typeof(Fighter).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, true);
            float beforeRun = player.transform.position.x;
            move.Invoke(player, new object[] { Vector3.right, 0.1f });
            Require(Mathf.Approximately(player.transform.position.x - beforeRun, BrawlerBalance.ElfRunSpeed * 0.1f),
                "実移動にエルフの走行速度を適用");
            typeof(Fighter).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, false);
            player.transform.position = Vector3.zero;
            MethodInfo range = typeof(BeltBrawler).GetMethod("IsInAttackRange", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (float facing in new[] { -1f, 1f })
            {
                typeof(Fighter).GetProperty("Facing").SetValue(player, facing);
                target.transform.position = Vector3.right * facing * (BrawlerBalance.ElfNormalAttackReach * 0.9f);
                Require((bool)range.Invoke(game, new object[] { player, target }), "左右の剣リーチ内に命中");
                target.transform.position = Vector3.right * facing
                    * ((BrawlerBalance.ElfNormalAttackReach + BrawlerBalance.PlayerAttackReach) / 2);
                Require(!(bool)range.Invoke(game, new object[] { player, target }), "ファイターの間合いでも剣リーチ外には命中しない");
            }
            typeof(Fighter).GetField("_attackKind", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, BrawlerDefines.AttackKind.Uppercut);
            Require(Mathf.Approximately(player.AttackReach, BrawlerBalance.UppercutReach * BrawlerBalance.ElfWeaponReachMultiplier),
                "エルフの特殊技にも職業のリーチ倍率を適用");
            player.SelectNextAction();
            Require(player.TryStoreThrowingKnife() && player.TryUseSelectedAction() && player.ThrowingKnivesHeld == 0,
                "エルフも共通所持品処理でナイフを消費");
            Require(!player.CanUseAction(BrawlerDefines.UsableAction.Healing), "エルフは回復魔法を使用不可");
        }
        finally
        {
            foreach (PlayerThrownProjectile projectile in UnityEngine.Object.FindObjectsByType<PlayerThrownProjectile>())
            {
                if ((Fighter)typeof(PlayerThrownProjectile).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(projectile) == player)
                {
                    UnityEngine.Object.DestroyImmediate(projectile.gameObject);
                }
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
            UnityEngine.Object.DestroyImmediate(player.gameObject);
            UnityEngine.Object.DestroyImmediate(target.gameObject);
        }
    }

    private static void ValidateThiefClassInput(Fighter sourcePlayer, Keyboard keyboard)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var player = (ThiefFighter)factory.CreateFighter("Test thief player", Vector3.zero, true, 1,
            playerClass: BrawlerDefines.PlayerClass.Thief);
        Fighter target = factory.CreateFighter("Test thief reach", Vector3.right, false, 1);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            Require(player.ClassSettings == PlayerClassCatalog.Thief
                && Array.Exists(player.GetComponentsInChildren<Transform>(), part => part.name == "Dagger"),
                "シーフの派生クラス・職業設定・短剣を生成");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F4));
            InputSystem.Update();
            Require(BrawlerInput.WasThiefSelected(), "F4でシーフを選択");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            SendState(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
            Require(!BrawlerInput.WasThiefSelected(), "通常の下移動では職業を変えない");
            SendState(pad, new GamepadState());
            SendState(pad, new GamepadState().WithButton(GamepadButton.Select).WithButton(GamepadButton.DpadDown));
            Require(BrawlerInput.WasThiefSelected(), "View・Shareと下でシーフを選択");
            SendState(pad, new GamepadState());
            MethodInfo move = typeof(Fighter).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic);
            move.Invoke(player, new object[] { Vector3.right, 0.1f });
            Require(Mathf.Approximately(player.transform.position.x, BrawlerBalance.ThiefWalkSpeed * 0.1f),
                "実移動にシーフの歩行速度を適用");
            typeof(Fighter).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, true);
            float beforeRun = player.transform.position.x;
            move.Invoke(player, new object[] { Vector3.right, 0.1f });
            Require(Mathf.Approximately(player.transform.position.x - beforeRun, BrawlerBalance.ThiefRunSpeed * 0.1f),
                "実移動にシーフの走行速度を適用");
            typeof(Fighter).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, false);
            player.transform.position = Vector3.zero;
            MethodInfo range = typeof(BeltBrawler).GetMethod("IsInAttackRange", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (float facing in new[] { -1f, 1f })
            {
                typeof(Fighter).GetProperty("Facing").SetValue(player, facing);
                target.transform.position = Vector3.right * facing * (BrawlerBalance.ThiefNormalAttackReach * 0.9f);
                Require((bool)range.Invoke(game, new object[] { player, target }), "左右の短剣リーチ内に命中");
                target.transform.position = Vector3.right * facing
                    * ((BrawlerBalance.ThiefNormalAttackReach + BrawlerBalance.PlayerAttackReach) / 2);
                Require(!(bool)range.Invoke(game, new object[] { player, target }), "ファイターの間合いでも短剣リーチ外には命中しない");
            }
            typeof(Fighter).GetField("_attackKind", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, BrawlerDefines.AttackKind.Uppercut);
            Require(Mathf.Approximately(player.AttackReach, BrawlerBalance.UppercutReach * BrawlerBalance.ThiefWeaponReachMultiplier),
                "シーフの特殊技にも職業のリーチ倍率を適用");
            Require(player.TryStoreThrowingKnife() && player.TryUseSelectedAction() && player.ThrowingKnivesHeld == 0,
                "シーフも共通所持品処理でナイフを消費");
            Require(!player.CanUseAction(BrawlerDefines.UsableAction.Healing), "シーフは魔法を使用しない");
        }
        finally
        {
            foreach (PlayerThrownProjectile projectile in UnityEngine.Object.FindObjectsByType<PlayerThrownProjectile>())
            {
                if ((Fighter)typeof(PlayerThrownProjectile).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(projectile) == player)
                {
                    UnityEngine.Object.DestroyImmediate(projectile.gameObject);
                }
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
            UnityEngine.Object.DestroyImmediate(player.gameObject);
            UnityEngine.Object.DestroyImmediate(target.gameObject);
        }
    }

    private static void ValidateDwarfClassInput(Fighter sourcePlayer, Keyboard keyboard)
    {
        var game = (BeltBrawler)typeof(Fighter).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourcePlayer);
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var player = (DwarfFighter)factory.CreateFighter("Test dwarf player", Vector3.zero, true, 1,
            playerClass: BrawlerDefines.PlayerClass.Dwarf);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            Require(player.ClassSettings == PlayerClassCatalog.Dwarf
                && Array.Exists(player.GetComponentsInChildren<Transform>(), part => part.name == "Axe blade"),
                "ドワーフの派生クラス・職業設定・斧を生成");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F3));
            InputSystem.Update();
            Require(BrawlerInput.WasDwarfSelected(), "F3でドワーフを選択");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            SendState(pad, new GamepadState().WithButton(GamepadButton.DpadUp));
            Require(!BrawlerInput.WasDwarfSelected(), "通常の上移動で職業を変えない");
            SendState(pad, new GamepadState());
            SendState(pad, new GamepadState().WithButton(GamepadButton.Select).WithButton(GamepadButton.DpadUp));
            Require(BrawlerInput.WasDwarfSelected(), "View・Shareと上でドワーフを選択");
            SendState(pad, new GamepadState());
            MethodInfo move = typeof(Fighter).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic);
            move.Invoke(player, new object[] { Vector3.right, 0.1f });
            Require(Mathf.Approximately(player.transform.position.x, BrawlerBalance.DwarfWalkSpeed * 0.1f),
                "実移動にドワーフの歩行速度を適用");
            SetPlayerTimer(player, "_runDirection", 1f);
            typeof(Fighter).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, true);
            float beforeRun = player.transform.position.x;
            move.Invoke(player, new object[] { Vector3.right, 0.1f });
            Require(Mathf.Approximately(player.transform.position.x - beforeRun, BrawlerBalance.DwarfRunSpeed * 0.1f),
                "実移動にドワーフの走行速度を適用");
            Require(player.AttackReach == BrawlerBalance.DwarfNormalAttackReach, "通常リーチを職業設定から取得");
            typeof(Fighter).GetField("_attackKind", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, BrawlerDefines.AttackKind.Uppercut);
            Require(Mathf.Approximately(player.AttackReach, BrawlerBalance.UppercutReach * BrawlerBalance.DwarfWeaponReachMultiplier),
                "特殊技はCSVリーチと職業倍率を組み合わせる");
            typeof(Fighter).GetMethod("Attack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            typeof(Fighter).GetMethod("UpdateAttackAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, new object[] { 0.01f });
            Transform arm = (Transform)typeof(Fighter).GetField("_attackArm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            Require(Quaternion.Angle(arm.localRotation, Quaternion.Euler(0, 0, BrawlerBalance.DwarfNormalAttackArmAngle)) < 0.01f,
                "通常攻撃の実モーションにドワーフの振り角を適用");
            foreach (float facing in new[] { -1f, 1f })
            {
                typeof(Fighter).GetProperty("Facing").SetValue(player, facing);
                typeof(Fighter).GetMethod("UpdateWalkAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, new object[] { Vector3.zero, 0.01f });
                Transform model = player.transform.Find("Model");
                Require(model.localScale.x == facing && model.localScale.y == BrawlerBalance.DwarfModelHeightScale
                    && player.transform.localScale == Vector3.one, "左右反転でも小柄なモデルを維持し、判定用の親は拡縮しない");
            }
            SetPlayerTimer(player, "_attackRemaining", 0);
            SetPlayerTimer(player, "_attackCooldown", 0);
            typeof(Fighter).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, false);
            Require(player.TryStoreThrowingKnife() && player.TryUseSelectedAction() && player.ThrowingKnivesHeld == 0,
                "ドワーフも共通所持品処理でナイフを消費");
            Require(!player.CanUseAction(BrawlerDefines.UsableAction.Healing), "ドワーフは魔法を使用しない");
        }
        finally
        {
            foreach (PlayerThrownProjectile projectile in UnityEngine.Object.FindObjectsByType<PlayerThrownProjectile>())
            {
                if ((Fighter)typeof(PlayerThrownProjectile).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(projectile) == player)
                {
                    UnityEngine.Object.DestroyImmediate(projectile.gameObject);
                }
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.RemoveDevice(pad);
            InputSystem.Update();
            UnityEngine.Object.DestroyImmediate(player.gameObject);
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

    // 補助魔法の直接入力・選択入力を通し、効果を詠唱前に付けないことを検証します。
    private static void ValidateSupportSpellInput()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<BeltBrawler>();
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(game);
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo feedback = typeof(ClericFighter).GetProperty("SpecialFeedbackColor", BindingFlags.Instance | BindingFlags.NonPublic);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            foreach (BrawlerDefines.UsableAction action in new[]
                { BrawlerDefines.UsableAction.Protection, BrawlerDefines.UsableAction.AttackBoost })
            {
                foreach (bool usePad in new[] { false, true })
                {
                    var caster = (ClericFighter)factory.CreateFighter("Test support spell", Vector3.zero, true, 1);
                    try
                    {
                        bool protection = action == BrawlerDefines.UsableAction.Protection;
                        float castSeconds = protection ? BrawlerBalance.ProtectionCastSeconds : BrawlerBalance.AttackBoostCastSeconds;
                        float effectSeconds = protection ? BrawlerBalance.ProtectionEffectSeconds : BrawlerBalance.AttackBoostEffectSeconds;
                        int otherUses = caster.Inventory.GetCount(protection
                            ? BrawlerDefines.UsableAction.AttackBoost : BrawlerDefines.UsableAction.Protection);
                        typeof(ClericFighter).GetProperty(protection ? "ProtectionUsesRemaining" : "AttackBoostUsesRemaining")
                            .SetValue(caster, 1);
                        caster.SelectNextAction();
                        if (!protection)
                        {
                            caster.SelectNextAction();
                        }
                        if (usePad)
                        {
                            SendState(pad, protection ? new GamepadState().WithButton(GamepadButton.East)
                                : new GamepadState { rightTrigger = 1 });
                        }
                        else
                        {
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState(protection ? Key.I : Key.E));
                            InputSystem.Update();
                        }
                        read.Invoke(caster, null);
                        Require(caster.IsCastingSpell && !caster.IsProtected && !caster.IsAttackBoosted
                            && !game.IsSpellEffectActive && caster.SelectedAction == BrawlerDefines.UsableAction.None,
                            "補助魔法は最後の1回でも予約し、詠唱中は効果と敵停止を開始しない");
                        Require(caster.CalculateMeleeDamage(BrawlerBalance.NormalDamage) == BrawlerBalance.NormalDamage,
                            "攻撃強化は詠唱中の威力を変更しない");
                        caster.SelectNextAction();
                        Require(!caster.TryProtection() && !caster.TryAttackBoost(), "詠唱中に他の魔法も重複使用できない");
                        AdvanceSpell(caster, 0);
                        AdvanceSpell(caster, castSeconds / 2);
                        Require(!caster.IsProtected && !caster.IsAttackBoosted, "詠唱途中でも補助効果を付けない");
                        AdvanceSpell(caster, caster.SpellCastSecondsRemaining);
                        Require(caster.IsReleasingSpell && game.IsSpellEffectActive
                            && (protection ? caster.IsProtected : caster.IsAttackBoosted),
                            "詠唱完了時に予約した補助効果を開始し、敵を停止する");
                        Require((protection ? caster.ProtectionSecondsRemaining : caster.AttackBoostSecondsRemaining)
                            == (protection ? BrawlerBalance.ProtectionSeconds : BrawlerBalance.AttackBoostSeconds),
                            "補助効果の持続時間は詠唱完了から開始する");
                        Color expected = protection ? new Color(1, 0.8f, 0.3f) : new Color(1, 0.45f, 0.2f);
                        Require((Color)feedback.GetValue(caster) == expected, "枠を切り替えても予約した魔法の演出色を維持する");
                        var visual = caster.GetComponentInChildren<SupportSpellEffect>();
                        Require(visual != null && visual.GetComponentsInChildren<LineRenderer>().Length
                            == (protection ? 1 : BrawlerBalance.SupportSpellBoostSymbolCount)
                            && visual.GetComponentsInChildren<Collider>().Length == 0,
                            "補助魔法ごとの形を生成し、エフェクトに接触判定を持たせない");
                        AdvanceSpell(caster, effectSeconds / 2);
                        typeof(SupportSpellEffect).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(visual, null);
                        Require(visual.GetComponentInChildren<LineRenderer>().startColor.a < 1
                            && visual.GetComponent<Light>().intensity > 0, "発動の進行率に同期して光と透明度を更新する");
                        AdvanceSpell(caster, effectSeconds / 2);
                        typeof(SupportSpellEffect).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(visual, null);
                        Require(!visual.gameObject.activeSelf, "発動演出終了時は後隙にエフェクトを残さない");
                        Require(!game.IsSpellEffectActive && caster.IsRecoveringFromSpell,
                            "補助魔法も演出終了時に敵を再開し、後隙へ移る");
                        AdvanceSpell(caster, BrawlerBalance.SpellRecoverySeconds);
                        typeof(ClericFighter).GetProperty(protection ? "ProtectionUsesRemaining" : "AttackBoostUsesRemaining")
                            .SetValue(caster, 1);
                        Require(caster.GetActionUseStatus(action) == (protection
                            ? BrawlerDefines.ActionUseStatus.ProtectionActive : BrawlerDefines.ActionUseStatus.AttackBoostActive)
                            && !(protection ? caster.TryProtection() : caster.TryAttackBoost())
                            && caster.Inventory.GetCount(action) == 1,
                            "効果中の再使用は追加消費も持続時間の延長も行わない");
                        typeof(ClericFighter).GetField(protection ? "_protectionRemaining" : "_attackBoostRemaining",
                            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(caster, 0f);
                        Require(protection ? caster.TryProtection() : caster.TryAttackBoost(), "中断検証用の詠唱を開始する");
                        caster.TakeDamage(1, caster.Facing);
                        Require(!caster.IsCastingSpell && caster.Inventory.GetCount(action) == 0 && !game.IsSpellEffectActive,
                            "補助魔法の詠唱中も被弾で中断し、回数を返却しない");
                        AdvanceSpell(caster, castSeconds + effectSeconds + BrawlerBalance.SpellRecoverySeconds);
                        Require(!caster.IsProtected && !caster.IsAttackBoosted
                            && caster.Inventory.GetCount(protection ? BrawlerDefines.UsableAction.AttackBoost
                                : BrawlerDefines.UsableAction.Protection) == otherUses,
                            "中断後は補助効果を遅れて発動せず、他の魔法回数を保持する");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(caster.gameObject);
                        SendState(pad, new GamepadState());
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        InputSystem.Update();
                    }
                }
            }
        }
        finally
        {
            InputSystem.RemoveDevice(pad);
            InputSystem.RemoveDevice(keyboard);
        }
    }

    // 共通基底クラスを経由し、回復の予約・中断・敵停止と入力をまとめて検証します。
    private static void ValidateHealingSpellInput()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<BeltBrawler>();
        var factory = (BrawlerSceneFactory)typeof(BeltBrawler).GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(game);
        var caster = (ClericFighter)factory.CreateFighter("Test healing spell", Vector3.zero, true, 1);
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        MethodInfo read = typeof(Fighter).GetMethod("ReadPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            foreach (bool usePad in new[] { false, true })
            {
                caster.TakeDamage(BrawlerBalance.HealingAmount, caster.Facing);
                SetPlayerTimer(caster, "_stunRemaining", 0);
                SetPlayerTimer(caster, "_attackCooldown", 0);
                int healthBefore = caster.Health;
                int usesBefore = caster.HealingUsesRemaining;
                if (usePad)
                {
                    SendState(pad, new GamepadState { rightTrigger = 1 });
                }
                else
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    InputSystem.Update();
                }
                read.Invoke(caster, null);
                Require(caster.IsCastingSpell && caster.Health == healthBefore
                    && caster.HealingUsesRemaining == usesBefore - 1 && !game.IsSpellEffectActive,
                    "キーボード・パッドの回復使用は詠唱を開始し、敵停止と回復はまだ起こさない");
                caster.SelectNextAction();
                Require(!caster.TryProtection() && !caster.TryHeal(), "詠唱中は他の魔法も回復の再使用も拒否する");
                AdvanceSpell(caster, 0);
                Require(caster.Health == healthBefore && caster.IsCastingSpell, "時間停止中は詠唱を進めない");
                AdvanceSpell(caster, BrawlerBalance.HealingCastSeconds);
                Require(caster.Health == caster.MaxHealth && caster.IsReleasingSpell && game.IsSpellEffectActive,
                    "枠を切り替えても予約した回復が一度だけ発動し、共通の敵停止に入る");
                var visual = caster.GetComponentInChildren<SupportSpellEffect>();
                Require(visual != null && visual.GetComponentsInChildren<LineRenderer>().Length == 3,
                    "回復は輪と十字の専用エフェクトを一度だけ生成する");
                AdvanceSpell(caster, BrawlerBalance.HealingEffectSeconds);
                typeof(SupportSpellEffect).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(visual, null);
                Require(!visual.gameObject.activeSelf, "回復演出も発動状態の終了と同時に消える");
                Require(!game.IsSpellEffectActive && caster.IsRecoveringFromSpell,
                    "回復演出終了時に停止を解除し、後隙中の敵は再開する");
                AdvanceSpell(caster, BrawlerBalance.SpellRecoverySeconds);
                Require(!caster.IsRecoveringFromSpell && caster.Health == caster.MaxHealth,
                    "後隙終了後にも回復を重複させない");
                for (int slot = 1; slot < caster.Inventory.SlotCount; slot++)
                {
                    caster.SelectNextAction();
                }
                SendState(pad, new GamepadState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
            }
            caster.TakeDamage(1, caster.Facing);
            SetPlayerTimer(caster, "_stunRemaining", 0);
            SetPlayerTimer(caster, "_attackCooldown", 0);
            Require(caster.TryHeal() && caster.SelectedAction == BrawlerDefines.UsableAction.None,
                "最後の回復は空き枠になっても詠唱を予約できる");
            int healthAtInterrupt = caster.Health;
            caster.TakeDamage(1, caster.Facing);
            Require(!caster.IsCastingSpell && caster.HealingUsesRemaining == 0 && !game.IsSpellEffectActive,
                "詠唱中の被弾は中断し、回数は返却せず敵も停止しない");
            AdvanceSpell(caster, BrawlerBalance.HealingCastSeconds + BrawlerBalance.HealingEffectSeconds
                + BrawlerBalance.SpellRecoverySeconds);
            Require(caster.Health == healthAtInterrupt - 1 && !caster.IsReleasingSpell,
                "中断後に時間が経過しても回復を発動させない");
        }
        finally
        {
            InputSystem.RemoveDevice(pad);
            InputSystem.RemoveDevice(keyboard);
            UnityEngine.Object.DestroyImmediate(caster.gameObject);
        }
    }

    private static void AdvanceSpell(SpellFighter caster, float seconds)
    {
        typeof(SpellFighter).GetMethod("UpdateSpell", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(caster, new object[] { seconds });
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
