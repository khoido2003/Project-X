using UnityEngine;

public class Character : MonoBehaviour
{
    [SerializeField]
    private GameObject visualPlaceHolder;

    [SerializeField]
    private CharacterData data;

    [SerializeField]
    private bool isPlayerControlled = false;

    private Transform weaponHolderTransform;

    [SerializeField]
    private Transform characterVisualHolder;

    private MovementComponent movementComponent;
    private WeaponComponent weaponComponent;
    private CharacterVisualComponent characterVisualComponent;
    private AnimationControllerComponent animationControllerComponent;

    private void Awake()
    {
        movementComponent = GetComponent<MovementComponent>();
        weaponComponent = GetComponent<WeaponComponent>();
        characterVisualComponent = GetComponent<CharacterVisualComponent>();
        animationControllerComponent = GetComponent<AnimationControllerComponent>();
    }

    private void Start()
    {
        Setup();
    }

    private void Setup()
    {
        visualPlaceHolder.SetActive(false);

        if (data == null)
        {
            Debug.LogError("Missing CharacterData!");
            return;
        }

        // NOTICE: this always spawned first so other component can find it correctly
        characterVisualComponent?.Initialize(data, characterVisualHolder);

        movementComponent?.Initialize(data.stats, isPlayerControlled);

        animationControllerComponent.Bind(movementComponent);

        // MUST find the weaponHolder here or else it will be null since the characterVisual has not spawned yet!
        WeaponHolder weaponHolder = GetComponentInChildren<WeaponHolder>();

        if (weaponHolder != null)
        {
            weaponHolderTransform = weaponHolder.transform;
            weaponComponent?.Initialize(data.weapon, weaponHolderTransform);
        }
    }
}
