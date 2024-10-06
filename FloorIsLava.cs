using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FloorIsLava;

public class FloorIsLava : Mod {
    public const string Localization = $"Mods.{nameof(FloorIsLava)}";

    public override void Load() {
        IL_Player.WingMovement += WingMovement;

        On_Player.Spawn_SetPositionAtWorldSpawn += OnSpawn_SetPositionAtWorldSpawn;
        On_Player.WingMovement += OnWingMovement;
    }

    public override void Unload() {
        IL_Player.WingMovement -= WingMovement;

        On_Player.Spawn_SetPositionAtWorldSpawn -= OnSpawn_SetPositionAtWorldSpawn;
        On_Player.WingMovement -= OnWingMovement;
    }

    private void OnSpawn_SetPositionAtWorldSpawn(On_Player.orig_Spawn_SetPositionAtWorldSpawn orig, Player self) {
        orig(self);
        if (FloorIsLavaConfig.GetInstance().SpawnPlayersInAir)
            self.position.Y -= 320;
    }

    private void OnWingMovement(On_Player.orig_WingMovement orig, Player self) {
        orig(self);
        if (FloorIsLavaConfig.GetInstance().NerfSoaringInsignia && self.empressBrooch && self.wingTime != 0)
            self.wingTime += 0.25f;
    }

    private void WingMovement(ILContext il) {
        try {
            ILCursor c = new(il);
            c.GotoNext(i => i.MatchLdarg0(),
                i => i.MatchLdarg0(),
                i => i.MatchLdfld(typeof(Player).GetField("wingTimeMax", BindingFlags.Public | BindingFlags.Instance)),
                i => i.MatchConvR4());
            c.Emit(OpCodes.Call, typeof(FloorIsLavaConfig).GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static, []));
            c.Emit(OpCodes.Call, typeof(FloorIsLavaConfig).GetMethod("get_NerfSoaringInsignia", BindingFlags.Public | BindingFlags.Instance));
            ILLabel ret = il.DefineLabel();
            c.Emit(OpCodes.Brtrue_S, ret);
            c.GotoNext(i => i.MatchRet());
            c.MarkLabel(ret);
            MonoModHooks.DumpIL(this, il);
        } catch (Exception e) {
            MonoModHooks.DumpIL(this, il);
            throw new ILPatchFailureException(this, il, e);
        }
    }
}

public class FloorIsLavaConfig : ModConfig {
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [DefaultValue(0f)]
    [Range(0f, 20f)]
    public float SpawnGracePeriod { get; set; }

    [DefaultValue(0)]
    [Range(0, int.MaxValue)]
    public int DeathDelay { get; set; }

    [DefaultValue(true)]
    public bool PlayersSpawnWithSquirrelHook { get; set; }

    [DefaultValue(true)]
    public bool SpawnPlayersInAir { get; set; }

    [DefaultValue(true)]
    [ReloadRequired]
    public bool NerfWings { get; set; }

    [DefaultValue(true)]
    [ReloadRequired]
    public bool NerfMounts { get; set; }

    [DefaultValue(false)]
    [ReloadRequired]
    public bool NerfMinecarts { get; set; }

    [DefaultValue(true)]
    [ReloadRequired]
    public bool NerfNeptunesShell { get; set; }

    [DefaultValue(true)]
    public bool NerfSoaringInsignia { get; set; }

    public static FloorIsLavaConfig GetInstance() => ModContent.GetInstance<FloorIsLavaConfig>();

    public static FloorIsLavaConfig GetInstance(out FloorIsLavaConfig cfg) {
        cfg = GetInstance();
        return cfg;
    }
}

public class GroundAllergicPlayer : ModPlayer {
    private int ticks = 0;
    private int ticksOnGround = 0;
    private List<int> hooks = [];
    private const int grappleLife = 1000;

    public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath) {
        if (Player.miscEquips[4].IsAir && FloorIsLavaConfig.GetInstance().PlayersSpawnWithSquirrelHook)
            Player.miscEquips[4] = new(ItemID.SquirrelHook);
        return [];
    }

    public override void PostUpdate() {
        ticks++;
        Vector2 feetPosition = Player.position + new Vector2(Player.width / 4, 9 * Player.height / 10 + 1);
        bool onTile = Collision.SolidTiles(feetPosition, Player.width / 2, Player.height / 10, true);
        
        if (ticks >= FloorIsLavaConfig.GetInstance(out var cfg).SpawnGracePeriod * 60 && onTile)
            ticksOnGround++;
        else
            ticksOnGround = 0;
        if (ticksOnGround > cfg.DeathDelay)
            Player.Hurt(PlayerDeathReason.ByCustomReason(Language.GetTextValue($"{FloorIsLava.Localization}.DeathMessages.TouchedGround_{Main.rand.Next(1, 7)}", Player.name)),
                42500 + Main.rand.Next(15000), 0);
    }

    public override void OnEnterWorld() {
        ticks = 0;
        ticksOnGround = 0;
    }

    public override void OnRespawn() {
        ticks = 0;
        ticksOnGround = 0;
    }
}

public class WingNerf : GlobalItem {
    public override bool AppliesToEntity(Item item, bool lateInstantiation) => ArmorIDs.Wing.Sets.Stats.IndexInRange(item.wingSlot) && ArmorIDs.Wing.Sets.Stats[item.wingSlot].FlyTime > 0;

    public override void SetDefaults(Item item) {
        if (FloorIsLavaConfig.GetInstance().NerfWings) {
            WingStats o = ArmorIDs.Wing.Sets.Stats[item.wingSlot];
            ArmorIDs.Wing.Sets.Stats[item.wingSlot] = new((int)(o.FlyTime / 1.4f), o.AccRunSpeedOverride, o.AccRunAccelerationMult, o.HasDownHoverStats, o.DownHoverSpeedOverride, o.DownHoverAccelerationMult);
        }
    }
}