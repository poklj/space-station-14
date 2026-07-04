
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Changeling.Fixtures;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Changeling;

[TestFixture]
[TestOf(typeof(ChangelingTransformSystem))]
public sealed class ChangelingSlimeTests : ChangelingTest
{
    private static readonly EntProtoId SlimeHumanoidProtoId = "MobSlimePerson";

    [SidedDependency(Side.Server)] private SharedChangelingIdentitySystem _changelingIdentity = default!;
    [SidedDependency(Side.Server)] private ChangelingTransformSystem _changelingTransform = default!;
    [SidedDependency(Side.Server)] private SharedStorageSystem _sharedStorage = default!;
    [SidedDependency(Side.Server)] private SharedTransformSystem _transform = default!;

    [SetUp]
    public override async Task Setup()
    {
        await base.Setup();

        // Set up the ling with a Slime present and already consumed.
        var slime = await SpawnTarget(SlimeHumanoidProtoId);
        var slimeEntity = ToServer(slime);
        var lingIdentityComp = Comp<ChangelingIdentityComponent>(Player);
        await Server.WaitPost(() =>
        {
            // Just give the ling the identity of a slime, no need to mess around with Devouring, that's on the devour test to handle.
            _changelingIdentity.GrantIdentity((SPlayer, lingIdentityComp),
                slimeEntity);
            Assert.That(lingIdentityComp.ConsumedIdentities, Has.Count.EqualTo(2));
        });
    }

    [Test]
    [Description(
        "Test that a Changeling transforming into a ling will gain the approprate storage container and BUI associated with the Species")]
    public async Task TransformIntoSlimeTest()
    {

        _changelingIdentity.TryGetDataFromOriginal(SPlayer,
            STarget!.Value,
            out var slimeIdentity);

        CUiSys.TryGetInterfaceData(CPlayer, StorageComponent.StorageUiKey.Key, out var beforeBuiData);
        Assert.That(HasComp<StorageComponent>(Player), Is.False);
        Assert.That(beforeBuiData, Is.Null);
        await Server.WaitPost(() =>
        {
            _changelingTransform.TransformInto(SPlayer, slimeIdentity!.Identity!.Value);

        });
        await AwaitDoAfters();

        //Check Storage and BUI Presence
        Assert.That(HasComp<StorageComponent>(Player), Is.True);
        CUiSys.TryGetInterfaceData(CPlayer, StorageComponent.StorageUiKey.Key, out var afterBuiStorageData);
        Assert.That(afterBuiStorageData, Is.Not.Null);
        CUiSys.TryGetInterfaceData(CPlayer, ChangelingTransformUiKey.Key, out var afterBuiTransformData);
        Assert.That(afterBuiTransformData, Is.Not.Null);
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
        CUiSys.TryGetInterfaceData(CPlayer, ChangelingTransformUiKey.Key, out var beforeBuiTransformData);
        Assert.That(beforeBuiTransformData, Is.Not.Null);

        // Transform the player back out
        await Server.WaitPost(() =>
        {
            _changelingTransform.TransformInto(SPlayer, humanIdentityData!.Identity!.Value);

        });
        await AwaitDoAfters();

        // Reassert that BUI's haven't been munged
        CUiSys.TryGetInterfaceData(CPlayer, ChangelingTransformUiKey.Key, out var afterBuiTransformData);
        Assert.That(afterBuiTransformData, Is.Not.Null);
        // And that Storage isn't present
        Assert.That(HasComp<StorageComponent>(Player), Is.False);
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
            // Just give the ling the identity of a slime, no need to mess around with Devouring, that's on the devour test to handle.
            _changelingIdentity.GrantIdentity((SPlayer, lingIdentityComp),
                secondSlimeEntity);
            Assert.That(lingIdentityComp.ConsumedIdentities, Has.Count.EqualTo(3));
            //just quickly pop into the slime identity
            _changelingTransform.TransformInto(SPlayer, slimeIdentity1!.Identity!.Value);
        });

        await AwaitDoAfters();
        _changelingIdentity.TryGetDataFromOriginal(SPlayer,
            secondSlimeEntity,
            out var slimeIdentity2);

        Assert.That(HasComp<StorageComponent>(Player), Is.True);
        //Spawn a Test Apple in the players hand
        await PlaceInHands("FoodApple");

        //Now stick it into our storage
        var storageComponent = Comp<StorageComponent>(Player);
        await Server.WaitPost(() =>
        {
            _sharedStorage.PlayerInsertHeldEntity(SPlayer, SPlayer);
        });
        Assert.That(storageComponent.StoredItems, Has.Count.EqualTo(1));


        //transform into the second slime we added earlier
        await Server.WaitPost(() =>
        {
            _changelingTransform.TransformInto(SPlayer, slimeIdentity2!.Identity!.Value);
        });
        await AwaitDoAfters();

        Assert.That(storageComponent.StoredItems, Has.Count.EqualTo(1));
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

        var apple = await PlaceInHands("FoodApple");
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
        Assert.That(HasComp<StorageComponent>(Player), Is.False);
        Assert.That(storageComponent.StoredItems, Has.Count.EqualTo(0));
        Assert.That(storageComponent.LifeStage, Is.EqualTo(ComponentLifeStage.Deleted));

        // Is the apple still alive?
        AssertExists(apple);
        Assert.That(_transform.InRange(SPlayer,appleEnt, 1), Is.True);
        Assert.That(_transform.GetParent(appleEnt), Is.Not.EqualTo(transformComponent));
    }
}
