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

    private MovementComponent movement;
    private WeaponComponent weaponComponent;
    private CharacterVisualComponent characterVisual;

    private void Awake()
    {
        visualPlaceHolder.SetActive(false);

        movement = GetComponent<MovementComponent>();
        weaponComponent = GetComponent<WeaponComponent>();
        characterVisual = GetComponent<CharacterVisualComponent>();
    }

    private void Start()
    {
        Setup();
    }

    private void Setup()
    {
        if (data != null)
        {
            characterVisual?.Initialize(data, characterVisualHolder);
            movement?.Initialize(data.stats, isPlayerControlled);

            WeaponHolder weaponHolder = GetComponentInChildren<WeaponHolder>();

            if (weaponHolder != null)
            {
                weaponHolderTransform = weaponHolder.transform;
                weaponComponent?.Initialize(data.weapon, weaponHolderTransform);
            }
        }
    }
}
