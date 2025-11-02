using UnityEngine;

public class UIRoot : MonoBehaviour
{
    [SerializeField]
    private HealthHUD_UI healthHUD;

    [SerializeField]
    private SkillBarUI skillBarUI;


    private World _world;

    private void Start()
    {
        _world = WorldRunner.Instance.World;
        skillBarUI.Bind(_world);
        healthHUD.Bind(_world);
    }
}
