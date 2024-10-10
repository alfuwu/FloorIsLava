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
        if (!ModLoader.HasMod("CalamityMod")) // calamity already does this exact IL edit, so skip doing it if calamity is loaded
            IL_Player.Update += Update;
        IL_Player.WingMovement += WingMovement;

        On_Player.GrappleMovement += OnGrappleMovement;
        On_Player.Spawn_SetPositionAtWorldSpawn += OnSpawn_SetPositionAtWorldSpawn;
        On_Player.WingMovement += OnWingMovement;
    }

    public override void Unload() {
        IL_Player.CheckDrowning -= CheckDrowning;
        if (!ModLoader.HasMod("CalamityMod"))
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
        self.position.Y -= FloorIsLavaConfig.GetInstance().SpawnHeightIncrease * 20;
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
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldfld, typeof(Player).GetField("accMerman", BindingFlags.Public | BindingFlags.Instance));
            c.Emit(OpCodes.Brfalse_S, vanilla);
            c.Emit(OpCodes.Ldc_I4, 8);
            c.Emit(OpCodes.Mul);
            c.Emit(OpCodes.Ldarg_0);
            // if the player is wearing a diving helmet, divide by four to make the combo not completely broken
            c.Emit(OpCodes.Ldfld, typeof(Player).GetField("accDivingHelm", BindingFlags.Public | BindingFlags.Instance));
            c.Emit(OpCodes.Brfalse_S, vanilla);
            c.Emit(OpCodes.Ldc_I4, 4);
            c.Emit(OpCodes.Div);
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
            c.GotoNext(MoveType.After, i => i.MatchLdarg0(),
                i => i.MatchLdfld(typeof(Player).GetField("empressBrooch", BindingFlags.Public | BindingFlags.Instance)));
            c.GotoNext(MoveType.After, i => i.MatchLdarg0(),
                i => i.MatchLdfld(typeof(Player).GetField("empressBrooch", BindingFlags.Public | BindingFlags.Instance)),
                i => i.MatchBrfalse(out skip));
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

    [DefaultValue(20)]
    [Range(0, int.MaxValue)]
    public int SpawnHeightIncrease { get; set; }

    [DefaultValue(true)]
    public bool ResetWingFlightTimeOnGrapple { get; set; }

    [DefaultValue(true)]
    public bool SofterFloorDetection { get; set; }

    [DefaultValue(true)]
    [ReloadRequired]
    public bool NerfWings { get; set; }

    [DefaultValue(true)]
    public bool NerfMounts { get; set; }

    [DefaultValue(false)]
    public bool NerfMinecarts { get; set; }

    [DefaultValue(true)]
    public bool NerfNeptunesShell { get; set; }

    [DefaultValue(true)]
    public bool NerfSoaringInsignia { get; set; }

    [DefaultValue(false)]
    [ReloadRequired]
    public bool NerfGrapplingHooks { get; set; }

    [DefaultValue(false)]
    public bool NerfLiquids { get; set; }

    [DefaultValue(false)]
    public bool ReallyNerfLiquids { get; set; }

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

    private  static bool SolidCollision(Vector2 position, int width, int height, bool acceptTopSurfaces) {
        int value = (int)(position.X / 16f) - 1;
        int value2 = (int)((position.X + width) / 16f) + 2;
        int value3 = (int)(position.Y / 16f) - 1;
        int value4 = (int)((position.Y + height) / 16f) + 2;
        int num = Utils.Clamp(value, 0, Main.maxTilesX - 1);
        value2 = Utils.Clamp(value2, 0, Main.maxTilesX - 1);
        value3 = Utils.Clamp(value3, 0, Main.maxTilesY - 1);
        value4 = Utils.Clamp(value4, 0, Main.maxTilesY - 1);
        Vector2 vector = default;
        for (int i = num; i < value2; i++) {
            for (int j = value3; j < value4; j++) {
                Tile tile = Main.tile[i, j];
                if (!tile.HasTile || tile.IsActuated)
                    continue;

                bool flag = Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
                if (acceptTopSurfaces)
                    flag |= Main.tileSolidTop[tile.TileType];

                if (flag) {
                    vector.X = i * 16;
                    vector.Y = j * 16;
                    int num2 = 16;
                    if (tile.IsHalfBlock) {
                        vector.Y += 8f;
                        num2 -= 8;
                    }

                    if (position.X + width > vector.X && position.X < vector.X + 16f && position.Y + height > vector.Y && position.Y < vector.Y + num2)
                        return true;
                }
            }
        }

        return false;
    }

    private static bool LiquidCollision(Vector2 position, int width, int height) {
        int value = (int)(position.X / 16f) - 1;
        int value2 = (int)((position.X + width) / 16f) + 2;
        int value3 = (int)(position.Y / 16f) - 1;
        int value4 = (int)((position.Y + height) / 16f) + 2;
        int num = Utils.Clamp(value, 0, Main.maxTilesX - 1);
        value2 = Utils.Clamp(value2, 0, Main.maxTilesX - 1);
        value3 = Utils.Clamp(value3, 0, Main.maxTilesY - 1);
        value4 = Utils.Clamp(value4, 0, Main.maxTilesY - 1);
        Vector2 vector;
        for (int i = num; i < value2; i++) {
            for (int j = value3; j < value4; j++) {
                if (Main.tile[i, j] != null && Main.tile[i, j].LiquidAmount > 0) {
                    vector.X = i * 16;
                    vector.Y = j * 16;
                    int num2 = 16;
                    float num3 = 256 - Main.tile[i, j].LiquidAmount;
                    num3 /= 32f;
                    vector.Y += num3 * 2f;
                    num2 -= (int)(num3 * 2f);
                    if (position.X + width > vector.X && position.X < vector.X + 16f && position.Y + height > vector.Y && position.Y < vector.Y + num2)
                        return true;
                }
            }
        }

        return false;
    }

    private bool FindItemByType(int itemType, out int index) => (index = Array.FindIndex(Player.inventory, item => item.type == itemType)) >= 0 || (index = Player.miscEquips[4].type == itemType ? -2 : -1) == -2;

    // god this is cursed, but it should be faster than the old version of the function
    private static string GetRandomPlayerName(Player excluded, IEnumerable<string> names = null) => Main.rand.NextFromList((names = Main.player.Where(p => p.active && !p.dead && p != excluded).Select(p => p.name)).Any() ? names.ToArray() : ["Bob", "Joe", "Dave", "Phillip", "Jerry", "Jasmine", "Sarah", "Miu", "Alfred", "Jesus", "Mother Teresa", "Fabsol", "Maxwell", "Ezkli", "Jeffrey"]);

    public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath) {
        if (FloorIsLavaConfig.GetInstance().PlayersSpawnWithSquirrelHook)
            if (Player.miscEquips[4].IsAir && !mediumCoreDeath)
                Player.miscEquips[4] = new(ItemID.SquirrelHook);
            else if (mediumCoreDeath && FindItemByType(ItemID.SquirrelHook, out int idx))
                if (idx == -2)
                    Player.miscEquips[4].TurnToAir();
                else
                    Player.inventory[idx].TurnToAir();
        return [];
    }

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
        if (Player.difficulty == PlayerDifficultyID.MediumCore && FloorIsLavaConfig.GetInstance().PlayersSpawnWithSquirrelHook)
            Player.miscEquips[4] = new(ItemID.SquirrelHook);
    }

    public override void PostUpdate() {
        ticks++;
        Vector2 feetPosition = Player.position + new Vector2(Player.width / 4, 9 * Player.gravDir * Player.height / 10 + Player.gravDir);
        int height = (int)(Player.gravDir * Player.height / 10);
        bool onTile = SolidCollision(feetPosition, Player.width / 2, height, true) && !Player.shimmering;
        bool inLiquid = LiquidCollision(Player.position + new Vector2(Player.width / 4, FloorIsLavaConfig.GetInstance(out var cfg).ReallyNerfLiquids ? Player.gravDir : 0), Player.width / 2, Player.height);

        foreach (Point p in Player.TouchedTiles)
            if (Main.tile[p.X, p.Y].HasTile)
                onTile = true; // helps detect slops/half blocks
        if (cfg.SofterFloorDetection)
            onTile &= Math.Abs(Player.velocity.Y) < 0.01f;

        if (ticks >= cfg.SpawnGracePeriod * 60 && (onTile || cfg.ReallyNerfLiquids && inLiquid))
            ticksOnGround++;
        else
            ticksOnGround = 0;
        if (ticksOnGround > cfg.DeathDelay && Main.myPlayer == Player.whoAmI)
            Player.Hurt(PlayerDeathReason.ByCustomReason(Language.GetTextValue($"{FloorIsLava.Localization}.DeathMessages.TouchedGround_{Main.rand.Next(64) + 1}", Player.name, GetRandomPlayerName(Player))),
                42500 + Main.rand.Next(15000), 0, dodgeable: false);
        else if (cfg.NerfLiquids && inLiquid) {
            Player.Hurt(PlayerDeathReason.ByOther(2), 20, 0, dodgeable: false);
            Player.AddBuff(BuffID.OnFire, 120);
        }
        if (Player.mount.Active && (cfg.NerfMounts && !MountID.Sets.Cart[Player.mount.Type] || MountID.Sets.Cart[Player.mount.Type] && cfg.NerfMinecarts)) {
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
    public override bool AppliesToEntity(Item item, bool lateInstantiation) => FloorIsLavaConfig.GetInstance().NerfWings && ArmorIDs.Wing.Sets.Stats.IndexInRange(item.wingSlot) && ArmorIDs.Wing.Sets.Stats[item.wingSlot].FlyTime > 0;

    public override void SetDefaults(Item item) {
        WingStats o = ArmorIDs.Wing.Sets.Stats[item.wingSlot];
        ArmorIDs.Wing.Sets.Stats[item.wingSlot] = new((int)(o.FlyTime / 1.2f), o.AccRunSpeedOverride, o.AccRunAccelerationMult, o.HasDownHoverStats, o.DownHoverSpeedOverride, o.DownHoverAccelerationMult);
    }
}

public class GrapplingHookNerf : GlobalProjectile {
    private static bool IsModdedGrapple(Projectile proj) { // i hope mods dont put unrelated logic in any of these functions
        if (proj.ModProjectile != null) {
            try {
                int nGrappleHooks = 3;
                float grappleRetreatSpeed = 11f;
                float grapplePullSpeed = 11f;
                float x = proj.position.X;
                float y = proj.position.Y;
                proj.ModProjectile.NumGrappleHooks(Main.player[proj.owner], ref nGrappleHooks);
                proj.ModProjectile.GrappleRetreatSpeed(Main.player[proj.owner], ref grappleRetreatSpeed);
                proj.ModProjectile.GrapplePullSpeed(Main.player[proj.owner], ref grapplePullSpeed);
                proj.ModProjectile.GrappleTargetPoint(Main.player[proj.owner], ref x, ref y);
                return nGrappleHooks != 3 || grappleRetreatSpeed != 11f || grapplePullSpeed != 11f || x != proj.position.X || y != proj.position.Y || proj.ModProjectile.CanUseGrapple(Main.player[proj.owner]) != null || proj.ModProjectile.GrappleRange() != 300 || proj.ModProjectile.GrappleCanLatchOnTo(Main.player[proj.owner], (int)proj.position.X / 16, (int)proj.position.Y / 16) != null;
            } catch {
                return true;
            }
        }
        return false;
    }

    public override bool AppliesToEntity(Projectile proj, bool lateInstantiation) => FloorIsLavaConfig.GetInstance().NerfGrapplingHooks && (ProjectileID.Sets.SingleGrappleHook[proj.type] || proj.aiStyle == ProjAIStyleID.Hook || IsModdedGrapple(proj));
    
    public override void SetDefaults(Projectile proj) => proj.timeLeft = 60;
}

// content
public class MountPhobia : ModBuff {
    public override void SetStaticDefaults() {
        Main.debuff[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex) {
        if (player.mount.Active && (FloorIsLavaConfig.GetInstance(out var cfg).NerfMounts && !MountID.Sets.Cart[player.mount.Type] || MountID.Sets.Cart[player.mount.Type] && cfg.NerfMinecarts))
            player.mount.Dismount(player);
    }
}