using UnityEngine;
using UnityEngine.InputSystem;

// キーボードとパッドの違いをこのクラスで吸収し、戦闘側にはゲームの操作として渡します。
// MonoBehaviour ではないため、シーンへの配置は不要です。
// Gamepad は機種別のボタン番号ではなく位置を表す API で操作を読み取れます。
public static class BrawlerInput
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureInputUpdates()
    {
        // Fighter は Update で操作を読むため、入力も各 Update の直前に更新する設定にそろえます。
        // 手動更新の設定が残っていても、通常のゲームループで入力が更新されるようにします。
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
    }

    public static bool IsGamepadConnected => Gamepad.current != null;

    // 実機入力が列挙されない状態を把握するための情報。仮想入力の検証とは区別します。
    public static bool HasInputDevice => Keyboard.current != null || Gamepad.current != null;

    // 左右が X、スティックの上下が Z。高さ Y は変更しません。
    public static Vector3 ReadMovement()
    {
        Vector2 keyboardMovement = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            keyboardMovement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            keyboardMovement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
        }

        Vector2 padMovement = Vector2.zero;
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            // leftStick は Input System のデッドゾーン処理を通ります。
            // 微小なスティックのずれでは動かず、倒した量は移動速度に反映されます。
            padMovement = pad.leftStick.ReadValue();
            Vector2 directionPad = pad.dpad.ReadValue();
            if (directionPad.sqrMagnitude > padMovement.sqrMagnitude)
            {
                padMovement = directionPad;
            }
        }

        // 同時入力は大きいほうを採用します。合算しないので、二重入力で速度が増えません。
        Vector2 movement = keyboardMovement.sqrMagnitude >= padMovement.sqrMagnitude
            ? keyboardMovement : padMovement;
        // normalized では浅い入力も最大速度になります。ClampMagnitude は小さい入力を保持します。
        movement = Vector2.ClampMagnitude(movement, 1);
        return new Vector3(movement.x, 0, movement.y);
    }

    public static bool WasAttackPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && (keyboard.jKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            || (pad != null && pad.buttonWest.wasPressedThisFrame);
    }

    public static bool WasDashPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && keyboard.leftShiftKey.wasPressedThisFrame)
            || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame));
    }

    public static bool WasRestartPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            || (pad != null && pad.startButton.wasPressedThisFrame);
    }

    // 回復は押した瞬間だけ発動します。長押しで使用回数を消費し続けません。
    public static bool WasHealPressed()
    {
        return (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
    }

    // 回復と独立したボタンにし、場面に応じて補助魔法を使い分けられるようにします。
    public static bool WasProtectionPressed()
    {
        return (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
    }

    // ガードだけは押した瞬間ではなく、押している間ずっと継続する操作です。
    public static bool IsGuardHeld()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && keyboard.kKey.isPressed)
            || (pad != null && (pad.leftShoulder.isPressed || pad.leftTrigger.isPressed));
    }
}
