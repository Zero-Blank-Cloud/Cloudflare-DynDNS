using System.Net;

public class Program
{
    static string CachedIP = "1";
    private static string[] Blacklist => File.ReadAllLines(Directory.GetCurrentDirectory() + "/Blacklist.txt");
    private static string Token => File.ReadAllText(Directory.GetCurrentDirectory() + "/Token.txt");
    public static void Main()
    {
        string? Token = Environment.GetEnvironmentVariable("Token");
        string[]? Zones = Environment.GetEnvironmentVariable("Zones")?.Split(';');
        string[]? StaticIP = Environment.GetEnvironmentVariable("StaticIP")?.Split(';');
        if (Token == null)
        {
            Console.WriteLine($"No Account Token Provided");
        }
        if (Zones == null)
        {
            Console.WriteLine("No ZoneIDs Provided");
        }
        while (true)
        {
            string ip = GetPublicIP();
            if (ip == "0")
            {
                Console.WriteLine("Couldnt Fetch Public IP!");
                Thread.Sleep(10000);
                continue;
            }
            if (ip != CachedIP)
            {
                CachedIP = ip;
                foreach (var Zone in Zones)
                {
                    try
                    {
                        var DNSRecords = ListDNSRecords(Zone);
                        foreach (var Record in DNSRecords.result)
                        {
                            try
                            {
                                if (Blacklist.Contains(Record.content)) continue;
                                if (Record.content == ip) continue;
                                if (Record.type != "A") continue;
                                var b = new CloudFlare_DynDNS.JSON.DNSRequest() { content = ip, name = Record.name, ttl = Record.ttl, type = "A", proxied = Record.proxied };
                                UpdateDNS(b, Record.id, Zone);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }

                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }
            Thread.Sleep(1000);
        }
    }
    static string GetPublicIP()
    {
        string ip = "";
        try
        {
            ip = new WebClient().DownloadString("https://api.ipify.org");
            return ip;
        }
        catch
        {
        }
        try
        {
            ip = new WebClient().DownloadString("https://ip.seeip.org");
        }
        catch
        {
            return "0";
        }
        return ip;
    }
    public static CloudFlare_DynDNS.JSON.ListDNSRecords ListDNSRecords(string zoneid)
    {
        Console.WriteLine("DNS List Request");
        var url = $"https://api.cloudflare.com/client/v4/zones/{zoneid}/dns_records";

        var httpRequest = (HttpWebRequest)WebRequest.Create(url);

        httpRequest.Headers["Authorization"] = "Bearer "+Token;


        var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
        using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<CloudFlare_DynDNS.JSON.ListDNSRecords>(streamReader.ReadToEnd());
        }
    }
    public static void UpdateDNS(CloudFlare_DynDNS.JSON.DNSRequest dns, string id, string zoneid)
    {
        var url = $"https://api.cloudflare.com/client/v4/zones/{zoneid}/dns_records/"+id;
        Console.WriteLine(url);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(dns, Newtonsoft.Json.Formatting.Indented);
        Console.WriteLine(json);
        var httpRequest = (HttpWebRequest)WebRequest.Create(url);
        httpRequest.Method = "PUT";
        httpRequest.Headers["Authorization"] = "Bearer "+Token;
        httpRequest.ContentType = "application/json";
        var data = json;
        using (var streamWriter = new StreamWriter(httpRequest.GetRequestStream()))
        {
            streamWriter.Write(data);
        }
        var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
        using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
        {
            var result = streamReader.ReadToEnd();
        }
        Console.WriteLine(httpResponse.StatusCode);
        Console.WriteLine($"Updated {dns.name} to {dns.content}");
    }
}