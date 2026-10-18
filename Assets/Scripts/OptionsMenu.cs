using UnityEngine;

// ゲーム入力より先に開閉を判断し、同じフレームの戦闘操作への流入を止めます。
[DefaultExecutionOrder(-200)]
public sealed class OptionsMenu : MonoBehaviour
{
    private static OptionsMenu s_active;
    private float _previousTimeScale;
    private int _blockedFrame = -1;
    private int _previousVertical;
    private int _previousHorizontal;
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    public bool IsOpen { get; private set; }
    public int SelectedOption { get; private set; }
    public static bool BlocksGameplay => s_active != null
        && (s_active.IsOpen || s_active._blockedFrame == Time.frameCount);

    private void Awake()
    {
        s_active = this;
    }

    private void Update()
    {
        if (!IsOpen)
        {
            if (BrawlerInput.WasOptionsPressed())
            {
                Open();
            }
            return;
        }
        if (BrawlerInput.WasOptionsCancelPressed())
        {
            Close();
            return;
        }
        int vertical = ReadVertical();
        if (vertical != 0 && vertical != _previousVertical)
        {
            SelectedOption = (SelectedOption - vertical + BrawlerBalance.OptionsRowCount) % BrawlerBalance.OptionsRowCount;
        }
        _previousVertical = vertical;
        int horizontal = ReadHorizontal();
        if (SelectedOption == BrawlerBalance.OptionsVolumeRow && horizontal != 0 && horizontal != _previousHorizontal)
        {
            AdjustVolume(horizontal);
        }
        _previousHorizontal = horizontal;
        if (BrawlerInput.WasOptionsConfirmPressed())
        {
            ActivateSelected();
        }
    }

    private static int ReadVertical()
    {
        float vertical = BrawlerInput.ReadMovement().z;
        return vertical > BrawlerBalance.OptionsInputThreshold ? 1
            : vertical < -BrawlerBalance.OptionsInputThreshold ? -1 : 0;
    }

    private static int ReadHorizontal()
    {
        float horizontal = BrawlerInput.ReadMovement().x;
        return horizontal > BrawlerBalance.OptionsInputThreshold ? 1
            : horizontal < -BrawlerBalance.OptionsInputThreshold ? -1 : 0;
    }

    private void AdjustVolume(int direction)
    {
        float before = AudioOptions.SoundEffectsVolume;
        AudioOptions.SoundEffectsVolume = before + direction * BrawlerBalance.SoundEffectsVolumeStep;
        if (!Mathf.Approximately(before, AudioOptions.SoundEffectsVolume))
        {
            PreviewVolume();
        }
    }

    private void PreviewVolume()
    {
        // 一時停止中も短い取得音で変更後の音量を確認できます。消音時は当然鳴りません。
        GetComponent<BrawlerAudio>().Play(BrawlerSound.Pickup);
    }

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }
        // ヒットストップやボス低速中の値も保存し、閉じた後は管理役がその続きから更新します。
        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0;
        IsOpen = true;
        SelectedOption = 0;
        _previousVertical = ReadVertical();
        _previousHorizontal = ReadHorizontal();
        _blockedFrame = Time.frameCount;
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }
        IsOpen = false;
        Time.timeScale = _previousTimeScale;
        _blockedFrame = Time.frameCount;
    }

    private void ActivateSelected()
    {
        switch (SelectedOption)
        {
            case 0:
                DisplayOptions.ShowLowSpellUsesWarning = !DisplayOptions.ShowLowSpellUsesWarning;
                break;
            case 1:
                DisplayOptions.ShowSpellDepletedNotice = !DisplayOptions.ShowSpellDepletedNotice;
                break;
            case BrawlerBalance.OptionsVolumeRow:
                PreviewVolume();
                break;
            case BrawlerBalance.OptionsCameraShakeRow:
                DisplayOptions.CameraShakeEnabled = !DisplayOptions.CameraShakeEnabled;
                break;
            default:
                Close();
                break;
        }
    }

    public void Draw(Font font)
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = BrawlerBalance.OptionsFontSize, alignment = TextAnchor.MiddleCenter };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = BrawlerBalance.OptionsFontSize };
        }
        _labelStyle.font = font;
        _buttonStyle.font = font;
        Color previousColor = GUI.color;
        if (!IsOpen)
        {
            if (GUI.Button(new Rect(Screen.width - BrawlerBalance.OptionsOpenButtonWidth - BrawlerBalance.SelectionHudMargin,
                Screen.height - BrawlerBalance.OptionsOpenButtonHeight - BrawlerBalance.OptionsOpenButtonMargin,
                BrawlerBalance.OptionsOpenButtonWidth, BrawlerBalance.OptionsOpenButtonHeight), "OPTIONS / Esc / R3"))
            {
                Open();
            }
            GUI.color = previousColor;
            return;
        }
        GUI.color = new Color(0, 0, 0, BrawlerBalance.OptionsOverlayAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        Matrix4x4 previousMatrix = GUI.matrix;
        float scale = Mathf.Min(1, (Screen.width - BrawlerBalance.OptionsMargin * 2) / BrawlerBalance.OptionsWidth,
            (Screen.height - BrawlerBalance.OptionsMargin * 2) / BrawlerBalance.OptionsHeight);
        scale = Mathf.Max(0, scale);
        GUI.matrix = previousMatrix * Matrix4x4.TRS(new Vector3((Screen.width - BrawlerBalance.OptionsWidth * scale) / 2,
            (Screen.height - BrawlerBalance.OptionsHeight * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
        GUI.color = new Color(0.06f, 0.09f, 0.14f, 1);
        GUI.DrawTexture(new Rect(0, 0, BrawlerBalance.OptionsWidth, BrawlerBalance.OptionsHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, 0, BrawlerBalance.OptionsWidth, BrawlerBalance.OptionsTitleHeight), "オプション", _labelStyle);
        for (int i = 0; i < BrawlerBalance.OptionsRowCount; i++)
        {
            if (i == BrawlerBalance.OptionsVolumeRow)
            {
                DrawVolumeRow(i);
                continue;
            }
            string label = i == 0 ? "残り回数の強調：" + (DisplayOptions.ShowLowSpellUsesWarning ? "ON" : "OFF")
                : i == 1 ? "使い切り表示：" + (DisplayOptions.ShowSpellDepletedNotice ? "ON" : "OFF")
                : i == BrawlerBalance.OptionsCameraShakeRow ? "画面の揺れ：" + (DisplayOptions.CameraShakeEnabled ? "ON" : "OFF")
                : "ゲームに戻る";
            GUI.color = i == SelectedOption ? Color.yellow : Color.white;
            if (GUI.Button(new Rect(BrawlerBalance.OptionsMargin, BrawlerBalance.OptionsTitleHeight + i * BrawlerBalance.OptionsRowHeight,
                BrawlerBalance.OptionsWidth - BrawlerBalance.OptionsMargin * 2, BrawlerBalance.OptionsButtonHeight),
                (i == SelectedOption ? "> " : "") + label, _buttonStyle))
            {
                SelectedOption = i;
                ActivateSelected();
            }
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(0, BrawlerBalance.OptionsTitleHeight + BrawlerBalance.OptionsRowCount * BrawlerBalance.OptionsRowHeight,
            BrawlerBalance.OptionsWidth, BrawlerBalance.OptionsHintHeight), "上下：選択　左右：音量　Enter / A：決定・試聴\nEsc / B / R3：閉じる", _labelStyle);
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private void DrawVolumeRow(int row)
    {
        bool selected = SelectedOption == row;
        float y = BrawlerBalance.OptionsTitleHeight + row * BrawlerBalance.OptionsRowHeight;
        float buttonWidth = BrawlerBalance.OptionsVolumeButtonWidth;
        float width = BrawlerBalance.OptionsWidth - BrawlerBalance.OptionsMargin * 2;
        GUI.color = selected ? Color.yellow : Color.white;
        if (GUI.Button(new Rect(BrawlerBalance.OptionsMargin, y, buttonWidth, BrawlerBalance.OptionsButtonHeight), "−", _buttonStyle))
        {
            SelectedOption = row;
            AdjustVolume(-1);
        }
        GUI.Label(new Rect(BrawlerBalance.OptionsMargin + buttonWidth, y, width - buttonWidth * 2,
            BrawlerBalance.OptionsButtonHeight), (selected ? "> " : "") + "効果音の音量："
            + Mathf.RoundToInt(AudioOptions.SoundEffectsVolume * BrawlerBalance.OptionsVolumePercent) + "%", _labelStyle);
        if (GUI.Button(new Rect(BrawlerBalance.OptionsMargin + width - buttonWidth, y, buttonWidth,
            BrawlerBalance.OptionsButtonHeight), "+", _buttonStyle))
        {
            SelectedOption = row;
            AdjustVolume(1);
        }
    }

    private void OnDisable()
    {
        Close();
        if (s_active == this)
        {
            s_active = null;
        }
    }
}
