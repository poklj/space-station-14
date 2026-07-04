using Content.IntegrationTests.Tests.Interaction;

namespace Content.IntegrationTests.Tests.Changeling.Fixtures;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract partial class ChangelingTest : InteractionTest
{
    protected override string PlayerPrototype => "MobLing";
}
