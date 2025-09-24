using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    [SerializeField]
    private GameObject visualPlaceHolder;

    public CharacterData Data;

    [SerializeField]
    private bool isPlayerControlled = false;

    private Transform weaponHolderTransform;

    [SerializeField]
    private Transform characterVisualHolder;

    private MovementComponent movementComponent;
    private WeaponComponent weaponComponent;
    private CharacterVisualComponent characterVisualComponent;
    private AnimationControllerComponent animationControllerComponent;
    private AttackComponent attackComponent;
    private HealthComponent healthComponent;
    private SkillComponent skillComponent;

    private HealthBarUI healthBarUI;

    private void Awake()
    {
        // Logic component
        movementComponent = GetComponent<MovementComponent>();
        weaponComponent = GetComponent<WeaponComponent>();
        characterVisualComponent = GetComponent<CharacterVisualComponent>();
        animationControllerComponent = GetComponent<AnimationControllerComponent>();
        attackComponent = GetComponent<AttackComponent>();
        healthComponent = GetComponent<HealthComponent>();
        skillComponent = GetComponent<SkillComponent>();

        // UI component (Optional)
        healthBarUI = GetComponentInChildren<HealthBarUI>();
    }

    private void Start()
    {
        Setup();
    }

    private void Setup()
    {
        visualPlaceHolder.SetActive(false);

        if (Data == null)
        {
            Debug.LogError("Missing CharacterData!");
            return;
        }

        // NOTICE: this always spawned first so other component can find it correctly
        characterVisualComponent?.Initialize(Data, characterVisualHolder);

        movementComponent?.Initialize(Data.stats, isPlayerControlled);

        healthComponent?.Initialize(Data.stats);
        healthBarUI.Bind(healthComponent);

        attackComponent?.Initialize(Data.weapon, isPlayerControlled);

        // ALL components that need animator will be listed here (Must implement IAnimationTrigger)
        List<IAnimationTrigger> animationTriggerSource = new() { movementComponent, attackComponent, skillComponent };

        animationControllerComponent?.Bind(animationTriggerSource);

        // MUST find the weaponHolder here or else it will be null since the characterVisual has not spawned yet!
        WeaponHolder weaponHolder = GetComponentInChildren<WeaponHolder>();

        if (weaponHolder != null)
        {
            weaponHolderTransform = weaponHolder.transform;
            weaponComponent?.Initialize(Data.weapon, weaponHolderTransform);
        }
    }
}
