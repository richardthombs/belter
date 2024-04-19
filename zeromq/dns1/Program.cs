using System.Net;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var e = Dns.GetHostEntry("ensek.com");
foreach (var a in e.AddressList)
{
	Console.WriteLine(a.ToString());
}