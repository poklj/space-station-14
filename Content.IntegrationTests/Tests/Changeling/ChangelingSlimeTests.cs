using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Content.IntegrationTests.NUnit.Constraints;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Input;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Changeling;

[TestOf(typeof(ChangelingTransformSystem))]
public sealed class ChangelingSlimeTests : InteractionTest
{
    protected override string PlayerPrototype => "MobLing";
    private static readonly EntProtoId SlimeHumanoidProtoId = "MobSlimePerson";
    private static readonly EntProtoId AppleProtoId = "FoodApple";

    [SidedDependency(Side.Server)] private SharedChangelingIdentitySystem _changelingIdentity = default!;
    [SidedDependency(Side.Server)] private ChangelingTransformSystem _changelingTransform = default!;
    [SidedDependency(Side.Server)] private SharedStorageSystem _sharedStorage = default!;
    [SidedDependency(Side.Server)] private SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private SharedContainerSystem _container = default!;
    public override async Task DoSetup()
    {
        await base.DoSetup();

        // Set up the ling with a Slime present and already consumed.
        var slime = await SpawnTarget(SlimeHumanoidProtoId);
        var slimeEntity = ToServer(slime);
        await Server.WaitPost(() =>
        {
            // Just give the ling the identity of a slime, no need to mess around with Devouring, that's on the devour test to handle.
            _changelingIdentity.GrantIdentity(SPlayer, slimeEntity);
        });
    }

    [Test]
    [Description(
        "Test that a Changeling transforming into a ling will gain the appropriate storage container and BUI associated with the Species")]
    public async Task TransformIntoSlimeTest()
    {

        _changelingIdentity.TryGetDataFromOriginal(SPlayer,
            STarget!.Value,
            out var slimeIdentity);

        Assert.That(CUiSys.TryGetInterfaceData(CPlayer, StorageComponent.StorageUiKey.Key, out _), Is.False);
        Assert.That(SPlayer, Has.No.Comp<StorageComponent>(Server));
        await Server.WaitPost(() =>
        {
            _changelingTransform.TransformInto(SPlayer, slimeIdentity!.Identity!.Value);

        });
        await AwaitDoAfters();

        //Check Storage and BUI Presence
        Assert.That(SPlayer, Has.Comp<StorageComponent>(Server));
        Assert.That(CUiSys.TryGetInterfaceData(CPlayer, StorageComponent.StorageUiKey.Key, out _), Is.True );
        Assert.That(CUiSys.TryGetInterfaceData(CPlayer, ChangelingTransformUiKey.Key, out _), Is.True);
    }

    [Test]
    [Description(
        "Test that a Changeling transforming out of a ling will remove the storage container and BUI associated with the Species Without disturbing other BUIs")]
    public async Task TransformOutOfSlimeTest()
    {
        // Transform ourselves into a slime, Previous test asserts that this gets us into a valid slime state.
        _changelingIdentity.TryGetDataFromOriginal(SPlayer,
            SPlayer,
            out var humanIdentityData);
        _changelingIdentity.TryGetDataFromOriginal(SPlayer,
            STarget!.Value,
            out var slimeIdentity);

        await Server.WaitPost(() =>
        {
            _changelingTransform.TransformInto(SPlayer, slimeIdentity!.Identity!.Value);

        });
        await AwaitDoAfters();

        // we can use Changelings BUI as an assertion that we aren't munging the set of BUI's on the player entity
        Assert.That(CUiSys.TryGetInterfaceData(CPlayer, ChangelingTransformUiKey.Key, out _), Is.True);

        // Transform the player back out
        await Server.WaitPost(() =>
        {
            _changelingTransform.TransformInto(SPlayer, humanIdentityData!.Identity!.Value);

        });
        await AwaitDoAfters();

        // Reassert that BUI's haven't been munged
        Assert.That(CUiSys.TryGetInterfaceData(CPlayer, ChangelingTransformUiKey.Key, out _), Is.True);

        // And that Storage isn't present
        Assert.That(SPlayer, Has.No.Comp<StorageComponent>(Server));
    }

    [Test]
    [Description(
        "Test that a Changeling transforming between slimes wont lose a storage")]
    public async Task TransformPreserveStorage()
    {
        var lingIdentityComp = Comp<ChangelingIdentityComponent>(Player);
        _changelingIdentity.TryGetDataFromOriginal(SPlayer,
            STarget!.Value,
            out var slimeIdentity1);

        //Spawn a second slime
        var secondSlime = await Spawn(SlimeHumanoidProtoId);
        var secondSlimeEntity = ToServer(secondSlime);

        await Server.WaitPost(() =>
        {
            _changelingIdentity.GrantIdentity(SPlayer, secondSlimeEntity);
            Assert.That(lingIdentityComp.ConsumedIdentities, Has.Count.EqualTo(3));
            //just quickly pop into the slime identity
            _changelingTransform.TransformInto(SPlayer, slimeIdentity1!.Identity!.Value);
        });


        await AwaitDoAfters();
        _changelingIdentity.TryGetDataFromOriginal(SPlayer,
            secondSlimeEntity,
            out var slimeIdentity2);

        Assert.That(SPlayer, Has.Comp<StorageComponent>(Server));
        //Spawn a Test Apple in the players hand
        var apple = await PlaceInHands(AppleProtoId);
        var appleEnt = ToServer(apple);
        //Now stick it into our storage
        var storageComponent = Comp<StorageComponent>(Player);
        await Server.WaitPost(() =>
        {
            _sharedStorage.PlayerInsertHeldEntity(SPlayer, SPlayer);
        });
        Assert.That(storageComponent.StoredItems, Has.Count.EqualTo(1));
        storageComponent.StoredItems.TryGetValue(appleEnt, out var appleStoredLocation);

        //transform into the second slime we added earlier
        await Server.WaitPost(() =>
        {
            _changelingTransform.TransformInto(SPlayer, slimeIdentity2!.Identity!.Value);
        });
        await AwaitDoAfters();

        //Check that it's the same component from earlier and that the apple is in the same container
        Assert.That(Comp<StorageComponent>(Player), Is.EqualTo(storageComponent));
        Assert.That(storageComponent.StoredItems, Is.Not.Null);
        Assert.That(storageComponent.StoredItems, Has.Count.EqualTo(1));

        _container.TryGetContainingContainer(appleEnt, out var container);
        Assert.That(container, Is.EqualTo(storageComponent.Container));
        storageComponent.StoredItems.TryGetValue(appleEnt, out var postTransformStoredLocation);
        Assert.That(appleStoredLocation, Is.EqualTo(postTransformStoredLocation));

        //Actually pull the apple from the inventory
        await Activate(Player);
        Assert.That(IsUiOpen(StorageComponent.StorageUiKey.Key), Is.True);
        var ctrl = GetStorageControl(apple);
        await ClickControl(ctrl, ContentKeyFunctions.MoveStoredItem);
        Assert.That(_container.TryGetContainingContainer(appleEnt, out container));
        Assert.That(container!.Owner, Is.EqualTo(SPlayer));

    }

    [Test]
    [Description(
        "Test that a changeling transforming out of a slime drops the item inside their storage onto the ground")]
    public async Task TransformDropStorage()
    {
        var transformComponent = Comp<TransformComponent>(Player);
        //Set up having an apple inside the slimes storage
        _changelingIdentity.TryGetDataFromOriginal(SPlayer,
            STarget!.Value,
            out var slimeIdentity);
        _changelingIdentity.TryGetDataFromOriginal(SPlayer,
            SPlayer,
            out var humanIdentity);

        var apple = await PlaceInHands(AppleProtoId);
        var appleEnt = ToServer(apple);

        await Server.WaitPost(() =>
        {
            _changelingTransform.TransformInto(SPlayer, slimeIdentity!.Identity!.Value);
        });
        await AwaitDoAfters();

        await Server.WaitPost(() =>
        {
            _sharedStorage.PlayerInsertHeldEntity(SPlayer, SPlayer);
        });
        Assert.That(_transform.GetParent(appleEnt), Is.EqualTo(transformComponent));

        //Now transform out
        var storageComponent = Comp<StorageComponent>(Player);
        await Server.WaitPost(() =>
        {
            _changelingTransform.TransformInto(SPlayer, humanIdentity!.Identity!.Value);
        });
        await AwaitDoAfters();

        //Assert that the storage container has been properly removed from the player,
        //items have been dumped and the lifestage for the StorageComponent has become Deleted
        Assert.That(SPlayer, Has.No.Comp<StorageComponent>(Server));
        Assert.That(storageComponent.StoredItems, Has.Count.EqualTo(0));
        Assert.That(storageComponent.LifeStage, Is.EqualTo(ComponentLifeStage.Deleted));

        // Is the apple still alive?
        Assert.That(_container.TryGetContainingContainer(appleEnt, out _), Is.False);
        Assert.That(_transform.InRange(SPlayer,appleEnt, 1), Is.True);
    }

    private ItemGridPiece GetStorageControl(NetEntity target)
    {
        var uid = ToClient(target);
        var hotbar = GetWidget<HotbarGui>();
        var storageContainer  = GetControlFromField<Control>(nameof(HotbarGui.SingleStorageContainer), hotbar);
        return GetControlFromChildren<ItemGridPiece>(c => c.Entity == uid, storageContainer);
    }
}
