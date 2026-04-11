using System;
using System.Threading.Tasks;

/// <summary>
/// Optimistic client prediction with host reconciliation. Apply a change instantly on the
/// client, send the request, rollback if the host rejects. Makes interactions feel zero-latency.
/// </summary>
/// <remarks>
/// <code>
/// await NetTransaction.Execute(
///     predict:  () =&gt; { money -= 100; UpdateHUD(); },
///     request:  new BuyReq { ItemId = "sword" },
///     rollback: () =&gt; { money += 100; ShowError("Failed"); }
/// );
/// </code>
/// </remarks>
public static class NetTransaction
{
	/// <summary>Response structs implement this so NetTransaction knows if the host accepted or rejected.</summary>
	public interface INetResponse
	{
		bool Success { get; }
	}

	/// <summary>
	/// Execute a predicted transaction. Predict runs immediately. If the host's response
	/// has <c>Success == false</c>, rollback fires.
	/// </summary>
	public static async Task<TResponse> Execute<TRequest, TResponse>(
		Action predict,
		TRequest request,
		Action rollback
	) where TResponse : INetResponse
	{
		predict?.Invoke();
		var response = await NetRequest.Ask<TRequest, TResponse>( request );
		if ( response == null || !response.Success )
			rollback?.Invoke();
		return response;
	}

	/// <summary>
	/// Execute with a custom success check instead of INetResponse.
	/// </summary>
	public static async Task<TResponse> Execute<TRequest, TResponse>(
		Action predict,
		TRequest request,
		Func<TResponse, bool> shouldRollback,
		Action rollback
	)
	{
		predict?.Invoke();
		var response = await NetRequest.Ask<TRequest, TResponse>( request );
		if ( shouldRollback != null && shouldRollback( response ) )
			rollback?.Invoke();
		return response;
	}
}
