using UnityEngine;

public abstract class SkillData : ScriptableObject
{
    public string skillName;
    public float coolDown = 5f;
    public float castRange = 5f;

    // True when only need press Key to active
    // False when need to press Key + click left mouse to activate the skill
    public bool isInstant = false;

    public AudioClip activateSound;
    public string activationAnimationTrigger = "skill";

    public ParticleSystem skillHitImpactEffectPrefab;
    public ParticleSystem skillHitImpactEffectInstance;

    public ParticleSystem skillVfxEffectPrefab;
    public ParticleSystem skillVfxEffectInstance;

    public abstract void Execute(GameObject owner, Vector3 targetPoint, Vector3 direction);

    public virtual void OnWeaponVfxEffectStart(GameObject owner) { }

    public virtual void OnWeaponVfxEffectStop(GameObject owner) { }

    public virtual void OnTriggerSkillVfxEffect() { }
}
