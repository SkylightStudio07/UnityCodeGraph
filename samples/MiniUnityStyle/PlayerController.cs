using UnityEngine;

namespace Sample.Gameplay;

public interface IDamageable
{
    void TakeDamage(int amount);
}

public sealed class PlayerController : MonoBehaviour, IDamageable
{
    [SerializeField] private HealthView healthView;
    private Weapon currentWeapon;

    private void Awake()
    {
        currentWeapon = GetComponent<Weapon>();
        var inventory = new InventoryModel(currentWeapon);
        healthView.Bind(inventory);
    }

    public void TakeDamage(int amount)
    {
        healthView.ShowDamage(amount);
    }
}
