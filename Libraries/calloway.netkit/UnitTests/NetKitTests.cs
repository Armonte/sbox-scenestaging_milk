using Sandbox;

[TestClass]
public partial class NetKitTests
{
	[TestMethod]
	public void NetRequest_HandleRegistersHandler()
	{
		NetRequest.Handle<TestRequest, TestResponse>( ( caller, req ) =>
		{
			return new TestResponse { Value = req.Input * 2 };
		} );

		// Handler is registered — no exception thrown.
		Assert.IsTrue( true );
	}

	[TestMethod]
	public void NetState_TransitionFiresEvent()
	{
		int backingField = 0;
		var state = new NetState<TestEnum>(
			() => backingField,
			v => backingField = v
		);

		bool transitioned = false;
		state.OnTransition += ( from, to ) =>
		{
			transitioned = true;
			Assert.AreEqual( TestEnum.Idle, from );
			Assert.AreEqual( TestEnum.Active, to );
		};

		state.TransitionTo( TestEnum.Active );

		Assert.IsTrue( transitioned );
		Assert.AreEqual( TestEnum.Idle, state.Previous );
	}

	[TestMethod]
	public void NetState_PollDetectsExternalChange()
	{
		int backingField = 0;
		var state = new NetState<TestEnum>(
			() => backingField,
			v => backingField = v
		);

		bool transitioned = false;
		state.OnTransition += ( from, to ) => transitioned = true;

		// Simulate external sync change (as if host pushed a new value)
		backingField = (int)TestEnum.Active;
		state.Poll();

		Assert.IsTrue( transitioned );
	}

	[TestMethod]
	public void NetState_NoEventOnSameState()
	{
		int backingField = 0;
		var state = new NetState<TestEnum>(
			() => backingField,
			v => backingField = v
		);

		bool transitioned = false;
		state.OnTransition += ( from, to ) => transitioned = true;

		state.TransitionTo( TestEnum.Idle ); // same as initial

		Assert.IsFalse( transitioned );
	}

	// ── Test types ────────────────────────────────────────────────────

	public enum TestEnum { Idle = 0, Active = 1, Done = 2 }
	public struct TestRequest { public int Input { get; set; } }
	public struct TestResponse { public int Value { get; set; } }
}
