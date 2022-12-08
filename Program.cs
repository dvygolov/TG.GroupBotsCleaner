using System.Linq;
using TL;

//YOUR SETTINGS START
var yourPhone = "+79222222222";
var chatName = "yourchat";
var minDate = new DateTime(2022, 12, 5, 17, 00, 0);
var maxDate = new DateTime(2022, 12, 6, 16, 00, 0);
var apiId = 00000;
var apiHash = "yourHash";
//YOUR SETTINGS STOP


//Do not modify anything starting from here!
WTelegram.Helpers.Log = (lvl, str) => System.Diagnostics.Debug.WriteLine(str);
using var client = new WTelegram.Client(apiId, apiHash);
await DoLogin(yourPhone);

var chats = await client.Messages_GetAllChats();
var chat = chats.chats.Values.Where(c => c is Channel).Cast<Channel>().FirstOrDefault(c => c.username == chatName);
if (chat == null)
{
    Console.WriteLine($"No chat with name {chatName} found!!!");
    return;
}
var fullInfo = await client.Channels_GetFullChannel(chat);
Console.WriteLine($"Chat to work on: {chat.ID} - {chat.Title}");
var filter = new ChannelAdminLogEventsFilter() { flags = ChannelAdminLogEventsFilter.Flags.join };
Channels_AdminLogResults log;
long maxId = File.Exists("maxid.txt") ? long.Parse(File.ReadAllText("maxid.txt")) : long.MaxValue;
Console.WriteLine($"Current MaxId: {maxId}");
do
{
    Console.WriteLine("Getting part of the log...");
    log = await client.Channels_GetAdminLog(chat, string.Empty, events_filter: filter, max_id: maxId);
    var events = log.events.Where(ev => ev.date >= minDate && ev.date < maxDate);
    foreach (var e in events)
    {
        if (fullInfo.users.ContainsKey(e.user_id))
        {
            Console.WriteLine(
                $"Removing user: {log.users[e.user_id]}, Join date: {e.date.ToString("dd-MM-yyyy HH:mm")}");
            try
            {
                var delRes = await client.DeleteChatUser(chat, log.users[e.user_id]);
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} Got flood warning, sleeping for {ex.X} seconds till {DateTime.Now.AddSeconds(ex.X).ToString("HH:mm:ss")}...");
                await Task.Delay(ex.X * 1000);
                Console.WriteLine("Work continued!");
                var delRes = await client.DeleteChatUser(chat, log.users[e.user_id]);
            }
        }
        else
            Console.WriteLine($"User {log.users[e.user_id]} (Joined {e.date.ToString("dd-MM-yyyy HH:mm")}) is already deleted!");
    }
    await Task.Delay(1000);
    if (log.events.Length == 0) break;
    maxId = log.events.Last().id;
    File.WriteAllText("maxid.txt", maxId.ToString());
}
while (log.events.First().date >= minDate);
Console.WriteLine("All that could be cleaned is cleaned! Thank you for using my script!");
Console.WriteLine("You can donate me smth here USDT TRC20: TKeNEVndhPSKXuYmpEwF4fVtWUvfCnWmra");


async Task DoLogin(string loginInfo) // (add this method to your code)
{
    while (client.User == null)
        switch (await client.Login(loginInfo)) // returns which config is needed to continue login
        {
            case "verification_code": Console.Write("Code: "); loginInfo = Console.ReadLine(); break;
            case "name": loginInfo = "John Doe"; break;    // if sign-up is required (first/last_name)
            case "password": loginInfo = "secret!"; break; // if user has enabled 2FA
            default: loginInfo = null; break;
        }
    Console.WriteLine($"We are logged-in as {client.User} (id {client.User.id})");
}
