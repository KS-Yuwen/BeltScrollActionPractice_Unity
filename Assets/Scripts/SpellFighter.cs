using UnityEngine;

// 魔法の詠唱・発動演出・後隙を担当します。効果の内容だけを職業の派生クラスで定義します。
public abstract class SpellFighter : ItemFighter
{
    private float _spellCastRemaining;
    private float _spellRecoveryRemaining;
    private float _spellEffectRemaining;
    private BrawlerDefines.UsableAction _castingAction;
    private bool _spellPoseInterrupted;
    private BrawlerDefines.UsableAction _activeSpell;
    private float _spellCastDuration;
    private float _spellEffectDuration;
    private float _spellRecoveryDuration;

    // 秒数を開始時に保存するため、所持枠の切り替えや消費による空き枠化に影響されません。
    protected bool TryBeginSpell(BrawlerDefines.UsableAction action, float castSeconds,
        float effectSeconds, float recoverySeconds)
    {
        if (GetActionUseStatus(action) != BrawlerDefines.ActionUseStatus.Ready || !Inventory.TryConsume(action))
        {
            return false;
        }
        _spellPoseInterrupted = false;
        _castingAction = action;
        _activeSpell = action;
        _spellCastDuration = Mathf.Max(0, castSeconds);
        _spellEffectDuration = Mathf.Max(0, effectSeconds);
        _spellRecoveryDuration = Mathf.Max(0, recoverySeconds);
        _spellCastRemaining = _spellCastDuration;
        ReleaseDefenseForSpecialAction();
        return true;
    }

    // 消費や時間管理を派生クラスに重複させず、詠唱完了時の効果を一度だけ呼び出します。
    protected abstract void ApplySpell(BrawlerDefines.UsableAction action);
    // 消費による空き枠化・選択切り替えと独立した、演出中の魔法の種類です。
    protected BrawlerDefines.UsableAction ActiveSpell => IsCastingSpell || IsReleasingSpell || IsRecoveringFromSpell
        ? _activeSpell : BrawlerDefines.UsableAction.None;
    protected virtual Color SpellEffectColor => Color.cyan;

    public bool IsCastingSpell => _castingAction != BrawlerDefines.UsableAction.None;
    public float SpellCastSecondsRemaining => _spellCastRemaining;
    public bool IsReleasingSpell => _spellEffectRemaining > 0;
    public float SpellEffectProgress => _spellEffectDuration > 0 ? 1 - _spellEffectRemaining / _spellEffectDuration : 1;
    public bool IsRecoveringFromSpell => _spellRecoveryRemaining > 0;
    protected override bool IsUsingSpecialAction => base.IsUsingSpecialAction || IsCastingSpell || IsReleasingSpell || IsRecoveringFromSpell;
    protected override Color? SpecialFeedbackColor => IsCastingSpell ? Color.yellow
        : IsReleasingSpell || IsRecoveringFromSpell ? SpellEffectColor : base.SpecialFeedbackColor;
    protected override float? SpecialActionArmAngle
    {
        get
        {
            if (_spellPoseInterrupted)
            {
                return base.SpecialActionArmAngle;
            }
            if (IsCastingSpell)
            {
                // 詠唱の終わりに腕を前へ振り出し、発動する瞬間には放つ姿勢になります。
                if (_spellCastRemaining <= BrawlerBalance.SpellReleaseArmSeconds)
                {
                    float releaseProgress = 1 - _spellCastRemaining / BrawlerBalance.SpellReleaseArmSeconds;
                    return Mathf.SmoothStep(BrawlerBalance.SpellCastingArmAngle,
                        BrawlerBalance.SpellReleaseArmAngle, releaseProgress);
                }
                float raiseProgress = (_spellCastDuration - _spellCastRemaining)
                    / BrawlerBalance.SpellRaiseArmSeconds;
                return Mathf.SmoothStep(BrawlerBalance.SpellReadyArmAngle,
                    BrawlerBalance.SpellCastingArmAngle, raiseProgress);
            }
            if (IsReleasingSpell)
            {
                return BrawlerBalance.SpellReleaseArmAngle;
            }
            if (IsRecoveringFromSpell)
            {
                // 発動直後は少し構えを維持し、後隙の残り時間で自然に通常姿勢へ戻します。
                // 演出専用のタイマーを増やさず、戦闘の後隙と常に同期させます。
                float elapsed = _spellRecoveryDuration - _spellRecoveryRemaining;
                float returnProgress = (elapsed - BrawlerBalance.SpellReleasePoseHoldSeconds)
                    / Mathf.Max(Mathf.Epsilon, _spellRecoveryDuration - BrawlerBalance.SpellReleasePoseHoldSeconds);
                return Mathf.SmoothStep(BrawlerBalance.SpellReleaseArmAngle,
                    BrawlerBalance.SpellReadyArmAngle, returnProgress);
            }
            return base.SpecialActionArmAngle;
        }
    }

    protected override void Update()
    {
        UpdateSpell(Time.deltaTime);
        base.Update();
    }

    // 経過時間を受け取り、低フレームレートでも余った時間を発動演出、後隙へ順に引き継ぎます。
    private void UpdateSpell(float deltaTime)
    {
        if (Health <= 0 || Game.IsRunFinished)
        {
            CancelSpellCast();
            _spellRecoveryRemaining = 0;
            _spellEffectRemaining = 0;
            return;
        }
        deltaTime = Mathf.Max(0, deltaTime);
        if (IsCastingSpell)
        {
            if (deltaTime < _spellCastRemaining)
            {
                _spellCastRemaining -= deltaTime;
                return;
            }
            deltaTime -= _spellCastRemaining;
            BrawlerDefines.UsableAction action = _castingAction;
            // 先に予約を解除し、次の更新で二重発動しないようにします。
            CancelSpellCast();
            // 先に発動状態へ移り、回復・攻撃のどちらでも同じ敵停止を使用します。
            _spellEffectRemaining = _spellEffectDuration;
            Game.BeginSpellEffect(this);
            ApplySpell(action);
            if (!IsReleasingSpell)
            {
                _spellRecoveryRemaining = _spellRecoveryDuration;
            }
        }

        if (IsReleasingSpell)
        {
            if (deltaTime < _spellEffectRemaining)
            {
                _spellEffectRemaining -= deltaTime;
                return;
            }
            deltaTime -= _spellEffectRemaining;
            _spellEffectRemaining = 0;
            _spellRecoveryRemaining = _spellRecoveryDuration;
        }
        _spellRecoveryRemaining = Mathf.Max(0, _spellRecoveryRemaining - deltaTime);
    }

    protected override void OnDamageReceived(int actualAmount)
    {
        base.OnDamageReceived(actualAmount);
        // 詠唱中の被弾で予約を取り消します。消費済みの回数は返却せず、被弾後に遅れて発動させません。
        CancelSpellCast();
        // 発動後の被弾でも腕の演出を解除し、被弾中に魔法を放つ構えだけ残さないようにします。
        _spellPoseInterrupted = true;
        _spellEffectRemaining = 0;
    }

    private void CancelSpellCast()
    {
        _castingAction = BrawlerDefines.UsableAction.None;
        _spellCastRemaining = 0;
    }
}
