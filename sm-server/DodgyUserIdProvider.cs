using Microsoft.AspNetCore.SignalR;

public class DodgyUserIdProvider : IUserIdProvider
{
	public virtual string GetUserId(HubConnectionContext connection)
	{
		return (connection.GetHttpContext()?.Request?.Query["access_token"]) ?? "Anonymous";
	}
}
