using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reproduces the retained training-dummy shape without entering Steam or loading a real match.
/// These tests exercise consumers of the NPC registry, not just the eligibility predicate.
/// </summary>
public class NpcOnlineIsolationTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<(FieldInfo field, object value)> singletonBackups =
        new List<(FieldInfo field, object value)>();
    private GameManager game;
    private HitboxManager hitboxes;
    private AnimationManager animation;
    private ProjectileManager projectiles;
    private ProjectileDictionary dictionary;

    [SetUp]
    public void SetUp()
    {
        game = CreateComponent<GameManager>();
        ReplaceSingleton(typeof(GameManager), "<Instance>k__BackingField", game);
        game.players = new PlayerController[4];
        game.playerCount = 0;
        game.playerNPCs = new List<PlayerController>();

        hitboxes = CreateComponent<HitboxManager>();
        ReplaceSingleton(typeof(HitboxManager), "<Instance>k__BackingField", hitboxes);
        animation = CreateComponent<AnimationManager>();
        ReplaceSingleton(typeof(AnimationManager), "<Instance>k__BackingField", animation);
        projectiles = CreateComponent<ProjectileManager>();
        ReplaceSingleton(typeof(ProjectileManager), "<Instance>k__BackingField", projectiles);
        dictionary = CreateComponent<ProjectileDictionary>();
        ReplaceSingleton(typeof(NonPersistantSingleton<ProjectileDictionary>), "instance", dictionary);
        SpellDictionary spells = CreateComponent<SpellDictionary>();
        ReplaceSingleton(typeof(NonPersistantSingleton<SpellDictionary>), "instance", spells);

        // DeleteProjectile uses the ordinary sound path; a muted source keeps that path inert.
        SFX_Manager sound = CreateComponent<SFX_Manager>();
        sound.soundObjects = new List<SFX_Manager.SoundObject>();
        AudioSource source = sound.GetComponent<AudioSource>();
        source.mute = true;
        SetField(sound, "menuSfxAudioSource", source);
        ReplaceSingleton(typeof(SFX_Manager), "<Instance>k__BackingField", sound);
    }

    [TearDown]
    public void TearDown()
    {
        if (projectiles != null)
        {
            foreach (BaseProjectile projectile in projectiles.projectilePrefabs)
            {
                if (projectile != null && !objects.Contains(projectile.gameObject))
                    objects.Add(projectile.gameObject);
            }
        }

        // Keep fixture singletons alive while PlayerController.OnDisable/OnDestroy runs.
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
        objects.Clear();
        for (int i = singletonBackups.Count - 1; i >= 0; i--)
            singletonBackups[i].field.SetValue(null, singletonBackups[i].value);
        singletonBackups.Clear();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OnlineAndBootstrap_ExcludeRetainedNpcsWithoutRenumberingHumanSlots(bool initializing)
    {
        PlayerController p1 = CreatePlayer("P1", 1);
        PlayerController vacantP2 = CreatePlayer("Vacant P2", 2);
        vacantP2.isConnected = false;
        vacantP2.isAlive = false;
        PlayerController p3 = CreatePlayer("P3", 3);
        game.players = new[] { p1, vacantP2, p3, null };
        game.playerCount = 3;
        game.playerNPCs.Add(CreatePlayer("Retained inactive dummy", 0, false));
        game.playerNPCs.Add(CreatePlayer("Retained active dummy", 0));
        EnterOnline(initializing);

        CollectionAssert.AreEqual(new[] { p1, vacantP2, p3 }, CollisionCandidates());
        CollectionAssert.AreEqual(new[] { p1, p3 }, RenderCandidates());
        Assert.That(game.GetPlayerByPID(0), Is.Null);
        Assert.That(game.GetPlayerByPID(3), Is.SameAs(p3));
    }

    [Test]
    public void Offline_UsesOnlyActiveEnabledNpcs_AndKeepsThemAvailableAfterReentry()
    {
        PlayerController inactive = CreatePlayer("Inactive training dummy", 0, false);
        PlayerController disabled = CreatePlayer("Disabled training dummy", 0);
        disabled.enabled = false;
        PlayerController active = CreatePlayer("Active training dummy", 0);
        game.playerNPCs.AddRange(new[] { null, inactive, disabled, active, active });
        Assert.That(inactive.gameObject.activeSelf, Is.True);
        Assert.That(inactive.gameObject.activeInHierarchy, Is.False);

        CollectionAssert.AreEqual(new[] { active }, CollisionCandidates());
        CollectionAssert.AreEqual(new[] { active }, RenderCandidates());
        Assert.That(game.GetPlayerByPID(0), Is.SameAs(active));

        active.gameObject.SetActive(false);
        Assert.That(CollisionCandidates(), Is.Empty);
        Assert.That(game.GetPlayerByPID(0), Is.Null);
        active.gameObject.SetActive(true);
        CollectionAssert.AreEqual(new[] { active }, CollisionCandidates());
        Assert.That(game.GetPlayerByPID(0), Is.SameAs(active));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OnlineSimulation_DoesNotTickRetainedOfflineNpc(bool initializing)
    {
        PlayerController dummy = CreatePlayer("Registered training dummy", 0);
        dummy.logicFrame = 123;
        game.playerNPCs.Add(dummy);
        EnterOnline(initializing);

        // A bare dummy has no stage/character setup: accidentally ticking it is itself a failure.
        game.UpdateGameState(Array.Empty<ulong>());

        Assert.That(dummy.logicFrame, Is.EqualTo(123));
    }

    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    public void ProjectilePool_ExcludesNpcsOnline_ButRetainsOfflineReentryPool(
        bool online, bool initializing, bool expectNpcPool)
    {
        PlayerController dummy = CreatePlayer("Inactive retained dummy", 0, false);
        SetProperty(dummy, "charData", new CharacterData { basicAttackProjId = "NpcIsolationProbe" });
        game.playerNPCs.Add(dummy);
        NpcIsolationProbeProjectile template = CreateComponent<NpcIsolationProbeProjectile>();
        dictionary.projectileDict.Add("NpcIsolationProbe", template);
        game.isOnlineMatchActive = online;
        if (initializing) SetField(game, "activeOnlineRoster", new OnlineMatchRoster());

        projectiles.InitializeAllProjectiles(rebuildExtraSpells: false);

        Assert.That(projectiles.projectilePrefabs.Count, Is.EqualTo(expectNpcPool ? 1 : 0));
        if (expectNpcPool)
        {
            Assert.That(projectiles.projectilePrefabs[0].owner, Is.SameAs(dummy));
            Assert.That(dummy.basicProjectileInstance, Is.SameAs(projectiles.projectilePrefabs[0].gameObject));
            Assert.That(projectiles.projectilePrefabs[0].gameObject.activeSelf, Is.False);
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void IneligibleNpcProjectile_IsRetiredBeforeSimulation(bool online, bool initializing)
    {
        PlayerController dummy = CreatePlayer("Dummy", 0, online || initializing);
        game.playerNPCs.Add(dummy);
        game.isOnlineMatchActive = online;
        if (initializing) SetField(game, "activeOnlineRoster", new OnlineMatchRoster());
        NpcIsolationProbeProjectile shot = AddActiveShot(dummy);
        shot.hitstop = 8;

        projectiles.UpdateProjectiles();

        Assert.That(shot.gameObject.activeSelf, Is.False);
        Assert.That(projectiles.activeProjectiles, Is.Empty);
        Assert.That(shot.updateCalls, Is.Zero);
        Assert.That(shot.resetCalls, Is.EqualTo(1));
        Assert.That(projectiles.projectilePrefabs[0], Is.SameAs(shot), "Pool indices must remain stable.");
    }

    [TestCase(false, 0)]
    [TestCase(true, 1)]
    public void EligibleOfflineNpcAndOnlineHumanProjectiles_StillSimulate(bool online, int pid)
    {
        PlayerController owner = CreatePlayer("Eligible owner", (short)pid);
        game.isOnlineMatchActive = online;
        NpcIsolationProbeProjectile shot = AddActiveShot(owner);

        projectiles.UpdateProjectiles();

        Assert.That(shot.gameObject.activeSelf, Is.True);
        Assert.That(shot.updateCalls, Is.EqualTo(1));
        Assert.That(shot.resetCalls, Is.Zero);
        CollectionAssert.AreEqual(new[] { shot }, projectiles.activeProjectiles);
    }

    [Test]
    public void DeleteNpcProjectiles_UsesEligibleNpcInsteadOfFirstStaleRegistryEntry()
    {
        PlayerController stale = CreatePlayer("Stale dummy", 0, false);
        PlayerController active = CreatePlayer("Current dummy", 0);
        game.playerNPCs.AddRange(new[] { stale, active });
        NpcIsolationProbeProjectile staleShot = AddActiveShot(stale);
        NpcIsolationProbeProjectile activeShot = AddActiveShot(active);

        projectiles.DeleteTargetPlayerProjectiles(0);

        Assert.That(activeShot.gameObject.activeSelf, Is.False);
        Assert.That(staleShot.gameObject.activeSelf, Is.True);
        CollectionAssert.AreEqual(new[] { staleShot }, projectiles.activeProjectiles);
    }

    [Test]
    public void DisconnectedHumanProjectiles_AreStillRemovedByTargetedCleanup()
    {
        PlayerController disconnected = CreatePlayer("Disconnected P2", 2);
        disconnected.isConnected = false;
        game.players[1] = disconnected;
        game.playerCount = 2;
        game.isOnlineMatchActive = true;
        NpcIsolationProbeProjectile shot = AddActiveShot(disconnected);

        projectiles.DeleteTargetPlayerProjectiles(2);

        Assert.That(shot.gameObject.activeSelf, Is.False);
        Assert.That(projectiles.activeProjectiles, Is.Empty);
        Assert.That(shot.resetCalls, Is.EqualTo(1));
    }

    private void EnterOnline(bool initializing)
    {
        game.isOnlineMatchActive = !initializing;
        if (initializing) SetField(game, "activeOnlineRoster", new OnlineMatchRoster());
    }

    private PlayerController CreatePlayer(string name, short pid, bool active = true)
    {
        GameObject obj = NewObject(name);
        obj.AddComponent<PlayerInput>().enabled = false;
        PlayerController player = obj.AddComponent<PlayerController>();
        player.pID = pid;
        player.input = InputConverter.ConvertFromLong(5);
        if (!active)
        {
            // SetStage hides the persistent stage parent, not the registered dummy itself.
            GameObject stage = NewObject("Inactive training stage");
            obj.transform.SetParent(stage.transform);
        }
        obj.SetActive(true);
        return player;
    }

    private NpcIsolationProbeProjectile AddActiveShot(PlayerController owner)
    {
        NpcIsolationProbeProjectile shot = CreateComponent<NpcIsolationProbeProjectile>();
        shot.owner = owner;
        shot.gameObject.SetActive(true);
        projectiles.projectilePrefabs.Add(shot);
        projectiles.activeProjectiles.Add(shot);
        return shot;
    }

    private T CreateComponent<T>() where T : Component => NewObject(typeof(T).Name).AddComponent<T>();

    private GameObject NewObject(string name)
    {
        GameObject obj = new GameObject("NpcIsolationTest_" + name);
        obj.SetActive(false);
        objects.Add(obj);
        return obj;
    }

    private PlayerController[] CollisionCandidates() =>
        (PlayerController[])typeof(HitboxManager).GetMethod("GetActivePlayerControllers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hitboxes, null);

    private PlayerController[] RenderCandidates() =>
        (PlayerController[])typeof(AnimationManager).GetMethod("GetFighters", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animation, null);

    private void ReplaceSingleton(Type type, string name, object value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, type.Name + "." + name);
        singletonBackups.Add((field, field.GetValue(null)));
        field.SetValue(null, value);
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void SetProperty(object target, string name, object value) =>
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetSetMethod(true).Invoke(target, new[] { value });
}

// A real component so the production pool can clone it; art and collision data are irrelevant here.
public sealed class NpcIsolationProbeProjectile : BaseProjectile
{
    public int updateCalls;
    public int resetCalls;
    public override void LoadProjectile() { }
    public override void ProjectileUpdate() => updateCalls++;
    public override void ResetValues() => resetCalls++;
}
