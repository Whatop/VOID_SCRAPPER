using UnityEngine;

public static class SeaRegionRuntimeApplier
{
    public static void ApplyToPlayer(GameObject playerObject, SeaRegionType regionType, bool logResult = true)
    {
        if (playerObject == null)
        {
            return;
        }

        SeaRegionDefinition definition = SeaRegionCatalog.Get(regionType);

        ApplyMovement(playerObject, definition);
        ApplyDash(playerObject, definition);
        ApplyWeapon(playerObject, definition);
        ApplyRuntimeBonus(playerObject, definition);

        if (logResult)
        {
            Debug.Log($"해역 효과 적용: {definition.DisplayName} / {definition.Description}", playerObject);
        }
    }

    private static void ApplyMovement(GameObject playerObject, SeaRegionDefinition definition)
    {
        if (Mathf.Approximately(definition.PlayerMoveSpeedPercent, 0f))
        {
            return;
        }

        PlayerController2D controller = playerObject.GetComponent<PlayerController2D>();

        if (controller == null)
        {
            return;
        }

        float multiplier = PercentToMultiplier(definition.PlayerMoveSpeedPercent);
        controller.SetMoveSpeed(controller.MoveSpeed * multiplier);
    }

    private static void ApplyDash(GameObject playerObject, SeaRegionDefinition definition)
    {
        if (Mathf.Approximately(definition.PlayerDashCooldownDelta, 0f))
        {
            return;
        }

        PlayerDash dash = playerObject.GetComponent<PlayerDash>();

        if (dash == null)
        {
            return;
        }

        dash.AddDashCooldown(definition.PlayerDashCooldownDelta);
    }

    private static void ApplyWeapon(GameObject playerObject, SeaRegionDefinition definition)
    {
        PlayerWeaponModifiers weaponModifiers = playerObject.GetComponent<PlayerWeaponModifiers>();

        if (weaponModifiers == null)
        {
            return;
        }

        if (!Mathf.Approximately(definition.PlayerDamagePercent, 0f))
        {
            weaponModifiers.AddDamagePercent(definition.PlayerDamagePercent);
        }

        if (!Mathf.Approximately(definition.PlayerProjectileSpeedPercent, 0f))
        {
            weaponModifiers.AddProjectileSpeedPercent(definition.PlayerProjectileSpeedPercent);
        }

        if (!Mathf.Approximately(definition.PlayerFireRatePercent, 0f))
        {
            weaponModifiers.AddFireRatePercent(definition.PlayerFireRatePercent);
        }
    }

    private static void ApplyRuntimeBonus(GameObject playerObject, SeaRegionDefinition definition)
    {
        PlayerRuntimeBonusState bonusState = playerObject.GetComponent<PlayerRuntimeBonusState>();

        if (bonusState == null)
        {
            bonusState = playerObject.AddComponent<PlayerRuntimeBonusState>();
        }

        if (!Mathf.Approximately(definition.ScrapGainPercent, 0f))
        {
            bonusState.AddScrapGainPercent(definition.ScrapGainPercent);
        }

        if (!Mathf.Approximately(definition.CreditsGainPercent, 0f))
        {
            bonusState.AddCreditsGainPercent(definition.CreditsGainPercent);
        }
    }

    private static float PercentToMultiplier(float percent)
    {
        return Mathf.Max(0.05f, 1f + percent * 0.01f);
    }
}