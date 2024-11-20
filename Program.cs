using TL;

Console.WriteLine("Telegram Bots Cleaner v.2.0 by Yellow Web");
Console.Write("Enter your phone number, for example +79222045502:");
var yourPhone = Console.ReadLine()??"";
Console.Write("Enter your chat/channel name without @, for example ohmyctr:");
var chatName = Console.ReadLine();
Console.Write("Enter your app id from https://my.telegram.org/apps:");
var apiId = int.Parse(Console.ReadLine()??"-1");
Console.Write("Enter your app hash from https://my.telegram.org/apps:");
var apiHash = Console.ReadLine();
Console.Write("Enter your 2FA key, if required. Otherwise just press Enter:");
var twofaSecret = Console.ReadLine();
Console.WriteLine("Enter start date and time to clear users in UTC0 timezone, for example 2024-11-22 22:00:00");
var minDate = DateTime.ParseExact(Console.ReadLine()??"","yyyy-MM-dd hh:mm:ss",null);
Console.WriteLine("Enter end date and time to clear users in UTC0 timezone, for example 2024-11-22 22:15:00");
var maxDate = DateTime.ParseExact(Console.ReadLine()??"","yyyy-MM-dd hh:mm:ss",null);

Console.WriteLine("Starting to work...");

//Do not modify anything starting from here!
//Supressing excessive logging
WTelegram.Helpers.Log = (lvl, str) => System.Diagnostics.Debug.WriteLine(str);
using var client = new WTelegram.Client(apiId, apiHash);
await DoLogin(yourPhone);

//Gettings our chat
var chats = await client.Messages_GetAllChats();
var chat = chats.chats.Values.Where(c => c is Channel).Cast<Channel>().FirstOrDefault(c => c.username == chatName);
if (chat == null)
{
    Console.WriteLine($"No chat with name {chatName} found!!!");
    return;
}
//Getting all chat members
var fullInfo = await client.Channels_GetFullChannel(chat);
Console.WriteLine($"Chat to work on: {chat.ID} - {chat.Title}");
//If we already save users - load them, else get them from Recent Acitons log
List<long> users =
    File.Exists("users.txt") ? File.ReadAllLines("users.txt").Select(long.Parse).ToList() : null;
if (users == null)
{
    //Getting Recent Actions log and filling new members list
    var filter = new ChannelAdminLogEventsFilter() { flags = ChannelAdminLogEventsFilter.Flags.join };
    long maxId = File.Exists("maxid.txt") ? long.Parse(File.ReadAllText("maxid.txt")) : long.MaxValue;
    Console.WriteLine($"Current MaxId: {maxId}");
    users = new List<long>();
    Console.WriteLine("Getting part of the log...");
    Channels_AdminLogResults log;
    do
    {
        log = await client.Channels_GetAdminLog(chat, string.Empty, events_filter: filter, max_id: maxId);
        if (log.events.Length == 0) break;
        var events = log.events.Where(ev => ev.date >= minDate && ev.date < maxDate);
        users.AddRange(events.Where(ev => fullInfo.users.ContainsKey(ev.user_id)).Select(ev => ev.user_id));
        maxId = log.events.Last().id;
        File.WriteAllText("maxid.txt", maxId.ToString());
    }
    while (log.events.First().date >= minDate);
    File.WriteAllLines("users.txt", users.Select(u => u.ToString()).ToArray());
}

//Cleaning all users
while (users.Count > 0)
{
    Console.WriteLine($"Removing user: {users[0]}");
    try
    {
        var delRes = await client.DeleteChatUser(chat, fullInfo.users[users[0]]);
    }
    catch (RpcException ex)
    {
        Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} Got flood warning, sleeping for {ex.X} seconds till {DateTime.Now.AddSeconds(ex.X).ToString("HH:mm:ss")}...");
        await Task.Delay(ex.X * 1000);
        Console.WriteLine("Work continued!");
        var delRes = await client.DeleteChatUser(chat, fullInfo.users[users[0]]);
    }
    users.RemoveAt(0);
    File.WriteAllLines("users.txt", users.Select(u => u.ToString()).ToArray());
}
Console.WriteLine("All that could be cleaned is cleaned! Thank you for using my script!");
Console.WriteLine("You can donate me smth here USDT TRC20: TKeNEVndhPSKXuYmpEwF4fVtWUvfCnWmra");


async Task DoLogin(string loginInfo) // (add this method to your code)
{
    while (client.User == null)
        switch (await client.Login(loginInfo)) // returns which config is needed to continue login
        {
            case "verification_code": Console.Write("Enter verification code from Telegram: "); loginInfo = Console.ReadLine(); break;
            case "name": loginInfo = "John Doe"; break;    // if sign-up is required (first/last_name)
            case "password": loginInfo = twofaSecret; break; // if user has enabled 2FA
            default: loginInfo = null; break;
        }
    Console.WriteLine($"We are logged-in as {client.User} (id {client.User.id})");
}
