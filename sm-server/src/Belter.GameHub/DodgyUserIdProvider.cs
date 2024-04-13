using Microsoft.AspNetCore.SignalR;

namespace Belter.GameHub;

public class DodgyUserIdProvider : IUserIdProvider
{
	public virtual string GetUserId(HubConnectionContext connection)
	{
		var userValues = connection.GetHttpContext()?.Request?.Query;
		if (userValues == null) return "Anonymous";
		return userValues["access_token"][0] ?? "Anonymous";
	}
}
