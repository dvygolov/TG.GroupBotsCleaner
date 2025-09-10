using System.Globalization;
using TG.GroupBotsCleaner;
using TL;
Copyright.Print();

Console.Write("Enter your phone number, for example +79222045502:");
var yourPhone = Console.ReadLine() ?? "";
Console.Write("Enter your chat/channel name without @, for example ohmyctr:");
var chatName = Console.ReadLine();
Console.Write("Enter your app id from https://my.telegram.org/apps:");
var apiId = int.Parse(Console.ReadLine() ?? "-1");
Console.Write("Enter your app hash from https://my.telegram.org/apps:");
var apiHash = Console.ReadLine();
Console.Write("Enter your 2FA key, if required. Otherwise just press Enter:");
var twofaSecret = Console.ReadLine();

string format = "yyyy-MM-dd HH:mm:ss";
Console.WriteLine("Enter start date and time to clear users in UTC0 timezone, for example 2024-11-22 22:00:00");
var startDate = Console.ReadLine() ?? "";
DateTime.TryParseExact(startDate, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime minDate);
Console.WriteLine("Enter end date and time to clear users in UTC0 timezone, for example 2024-11-22 22:15:00");
var endDate = Console.ReadLine() ?? "";
DateTime.TryParseExact(endDate, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime maxDate);

Console.WriteLine("Starting to work...");

//Do not modify anything starting from here!
//Supressing excessive logging
WTelegram.Helpers.Log = (lvl, str) => System.Diagnostics.Debug.WriteLine(str);
using var client = new WTelegram.Client(apiId, apiHash);
await DoLogin(yourPhone);

//Gettings our chat
Console.WriteLine("Getting ALL chats, please WAIT...");
var chats = await client.Messages_GetAllChats();
var chat = chats.chats.Values.Where(c => c is Channel).Cast<Channel>().FirstOrDefault(c => c.username == chatName);
if (chat == null)
{
    Console.WriteLine($"No chat/channel with name {chatName} found!!!");
    return;
}
Console.WriteLine($"Found chat to work on: {chat.ID} - {chat.Title}");
Console.WriteLine($"Getting ALL chat/channel members, please WAIT...");
//Getting all chat members
var participants = await client.Channels_GetAllParticipants(chat);
Console.WriteLine($"Found {participants.count} users in this chat/channel");
//If we already save users - load them, else get them from Recent Acitons log
List<long>? users = null;
if (File.Exists("users.txt"))
{
    Console.WriteLine("Found users.txt file, reading users that will be banned from there...");
    users = File.ReadAllLines("users.txt").Select(long.Parse).ToList();
    Console.WriteLine($"Found {users.Count} users.");
}

if (users == null)
{
    //Getting Recent Actions log and filling new members list
    var filter = new ChannelAdminLogEventsFilter() { flags = ChannelAdminLogEventsFilter.Flags.join };
    long maxId = long.MaxValue;
    if (File.Exists("maxid.txt"))
    {
        Console.WriteLine("Found maxid.txt file, reading max id from there.");
        maxId = long.Parse(File.ReadAllText("maxid.txt"));
    }

    Console.WriteLine($"Current MaxId: {maxId}");
    users = new List<long>();
    Console.WriteLine("Getting part of the log...");
    Channels_AdminLogResults log;
    do
    {
        log = await client.Channels_GetAdminLog(chat, string.Empty, events_filter: filter, max_id: maxId);
        if (log.events.Length == 0) break;
        var events = log.events.Where(ev => ev.date >= minDate && ev.date < maxDate).ToList();
        users.AddRange(events.Select(ev => ev.user_id));
        maxId = log.events.Last().id;
        File.WriteAllText("maxid.txt", maxId.ToString());
    }
    while (log.events.First().date >= minDate);
    File.WriteAllLines("users.txt", users.Select(u => u.ToString()).ToArray());
}

//Cleaning all users
while (users.Count > 0)
{
    try
    {
        if (!participants.users.ContainsKey(users[0]))
        {
            users.RemoveAt(0);
            continue;
        }

        var tgUser = participants.users[users[0]];
        Console.WriteLine($"Removing user: {tgUser.MainUsername} with id {users[0]}...");
        var delRes = await client.DeleteChatUser(chat,tgUser);
    }
    catch (RpcException ex)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} Got flood warning, sleeping for {ex.X} seconds till {DateTime.Now.AddSeconds(ex.X):HH:mm:ss}...");
        await Task.Delay(ex.X * 1000);
        Console.WriteLine("Work continued!");
        var delRes = await client.DeleteChatUser(chat, participants.users[users[0]]);
    }
    users.RemoveAt(0);
    File.WriteAllLines("users.txt", users.Select(u => u.ToString()).ToArray());
}
Console.WriteLine("All that could be cleaned is cleaned! Thank you for using my script!");
Console.WriteLine("You can donate me smth here USDT TRC20: TKeNEVndhPSKXuYmpEwF4fVtWUvfCnWmra");
File.Delete("users.txt");
File.Delete("maxid.txt");


async Task DoLogin(string? loginInfo)
{
    while (client.User == null)
        switch (await client.Login(loginInfo))
        {
            case "verification_code":
                Console.Write("Enter verification code from Telegram: ");
                loginInfo = Console.ReadLine();
                break;
            case "name":
                Console.Write("This shouldn't happen, NAME should be already set!");
                loginInfo = "John Doe";
                break;
            case "password":
                loginInfo = twofaSecret;
                break;
            default:
                loginInfo = null;
                break;
        }
    Console.WriteLine($"We are logged-in as {client.User} (id {client.User.id})");
}
