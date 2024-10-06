using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FloorIsLava;

// yippee massive file
public class FloorIsLava : Mod {
    private static readonly MethodInfo getConfig = typeof(FloorIsLavaConfig).GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static, []);

    public const string Localization = $"Mods.{nameof(FloorIsLava)}";

    public override void Load() {
        IL_Player.CheckDrowning += CheckDrowning;
        IL_Player.Update += Update;
        IL_Player.WingMovement += WingMovement;

        On_Player.GrappleMovement += OnGrappleMovement;
        On_Player.Spawn_SetPositionAtWorldSpawn += OnSpawn_SetPositionAtWorldSpawn;
        On_Player.WingMovement += OnWingMovement;
    }

    public override void Unload() {
        IL_Player.CheckDrowning -= CheckDrowning;
        IL_Player.Update -= Update;
        IL_Player.WingMovement -= WingMovement;

        On_Player.GrappleMovement -= OnGrappleMovement;
        On_Player.Spawn_SetPositionAtWorldSpawn -= OnSpawn_SetPositionAtWorldSpawn;
        On_Player.WingMovement -= OnWingMovement;
    }

    private void OnGrappleMovement(On_Player.orig_GrappleMovement orig, Player self) {
        orig(self);
        if (FloorIsLavaConfig.GetInstance().ResetWingFlightTimeOnGrapple && self.grappling[0] > -1)
            self.wingTime = self.wingTimeMax;
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

    private void CheckDrowning(ILContext il) {
        try {
            ILCursor c = new(il);
            ILLabel skip = null;
            c.GotoNext(i => i.MatchLdarg0(),
                i => i.MatchLdfld(typeof(Player).GetField("accMerman", BindingFlags.Public | BindingFlags.Instance)),
                i => i.MatchBrfalse(out skip));
            c.Emit(OpCodes.Call, getConfig);
            c.Emit(OpCodes.Call, ConfigVariable("NerfNeptunesShell"));
            c.Emit(OpCodes.Brtrue_S, skip);
            c.GotoNext(MoveType.After, i => i.MatchCall(typeof(Player).GetMethod("get_breathCDMax", BindingFlags.Public | BindingFlags.Instance)));
            ILLabel vanilla = il.DefineLabel();
            c.Emit(OpCodes.Call, getConfig);
            c.Emit(OpCodes.Call, ConfigVariable("NerfNeptunesShell"));
            c.Emit(OpCodes.Brfalse_S, vanilla);
            c.Emit(OpCodes.Ldc_I4, 8);
            c.Emit(OpCodes.Mul);
            c.MarkLabel(vanilla);
        } catch (Exception e) {
            MonoModHooks.DumpIL(this, il);
            throw new ILPatchFailureException(this, il, e);
        }
    }

    private void Update(ILContext il) {
        try {
            ILCursor c = new(il);
            ILLabel skip = null;
            c.GotoNext(i => i.MatchLdarg0(),
                i => i.MatchLdfld(typeof(Player).GetField("empressBrooch", BindingFlags.Public | BindingFlags.Instance)),
                i => i.MatchBrfalse(out skip),
                i => i.MatchLdarg0(),
                i => i.MatchLdarg0(),
                i => i.MatchLdfld(typeof(Player).GetField("rocketTimeMax", BindingFlags.Public | BindingFlags.Instance)));
            c.GotoNext(i => i.MatchLdarg0());
            c.Emit(OpCodes.Call, getConfig);
            c.Emit(OpCodes.Call, ConfigVariable("NerfSoaringInsignia"));
            c.Emit(OpCodes.Brtrue_S, skip);
        } catch (Exception e) {
            MonoModHooks.DumpIL(this, il);
            throw new ILPatchFailureException(this, il, e);
        }
    }

    private void WingMovement(ILContext il) {
        try {
            ILCursor c = new(il);
            c.GotoNext(i => i.MatchLdarg0(),
                i => i.MatchLdarg0(),
                i => i.MatchLdfld(typeof(Player).GetField("wingTimeMax", BindingFlags.Public | BindingFlags.Instance)),
                i => i.MatchConvR4());
            ILLabel ret = il.DefineLabel();
            c.Emit(OpCodes.Call, getConfig);
            c.Emit(OpCodes.Call, ConfigVariable("NerfSoaringInsignia"));
            c.Emit(OpCodes.Brtrue_S, ret);
            c.GotoNext(i => i.MatchRet());
            c.MarkLabel(ret);
        } catch (Exception e) {
            MonoModHooks.DumpIL(this, il);
            throw new ILPatchFailureException(this, il, e);
        }
    }

    private static MethodInfo ConfigVariable(string name) => typeof(FloorIsLavaConfig).GetMethod("get_" + name, BindingFlags.Public | BindingFlags.Instance);
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
    public bool ResetWingFlightTimeOnGrapple { get; set; }

    [DefaultValue(true)]
    [ReloadRequired]
    public bool NerfWings { get; set; }

    [DefaultValue(true)]
    public bool NerfMounts { get; set; }

    [DefaultValue(false)]
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
    private int mountTime = 0;
    private const int maxMountTime = 1800; // 30 sec

    private static string GetRandomPlayerName(Player excluded) => Main.rand.NextFromList(Main.player.Where(p => p.active && !p.dead && p != excluded).Any() ? Main.player.Where(p => p.active && !p.dead && p != excluded).Select(p => p.name).ToArray() : ["Bob", "Joe", "Dave", "Phillip", "Jerry", "Jasmine", "Sarah", "Miu", "Alfred", "Jesus", "Mother Teresa", "Fabsol", "Maxwell", "Ezkli", "Jeffrey"]);

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
            Player.Hurt(PlayerDeathReason.ByCustomReason(Language.GetTextValue($"{FloorIsLava.Localization}.DeathMessages.TouchedGround_{Main.rand.Next(1, 25)}", Player.name, GetRandomPlayerName(Player))),
                42500 + Main.rand.Next(15000), 0);
        if (Player.mount.Active && (cfg.NerfMounts && !MountID.Sets.Cart[Player.mount.Type] || !cfg.NerfMounts && cfg.NerfMinecarts)) {
            mountTime++;
        } else if (mountTime > 0) {
            mountTime = 0;
            Player.AddBuff(ModContent.BuffType<MountPhobia>(), Math.Max(300, (int)(600 * (float)mountTime / maxMountTime)));
        }
        if (mountTime > maxMountTime) {
            Player.mount.Dismount(Player);
            mountTime = 0;
            Player.AddBuff(ModContent.BuffType<MountPhobia>(), 600);
        }
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
            ArmorIDs.Wing.Sets.Stats[item.wingSlot] = new((int)(o.FlyTime / 1.2f), o.AccRunSpeedOverride, o.AccRunAccelerationMult, o.HasDownHoverStats, o.DownHoverSpeedOverride, o.DownHoverAccelerationMult);
        }
    }
}

// content
public class MountPhobia : ModBuff {
    public override void SetStaticDefaults() {
        Main.debuff[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex) {
        if (player.mount.Active)
            player.mount.Dismount(player);
    }
}