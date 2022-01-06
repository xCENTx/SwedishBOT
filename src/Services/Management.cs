using System;

namespace SwedishBOT.Services
{
    public static class Admins
    {
        //  These are account ID's. Due to being a public source the ID's have been set to 0
        public static ulong Swedish = 0;
        public static ulong Prince = 0;
        public static ulong TheFrieber = 0;
    }

    public static class Roles
    {
        public static ulong sub = 721123742373118014;
        public static ulong Trusted = 869666748973088828;
        public static ulong Mod = 748574813035167847;
        public static ulong SwedishTwat = 720940414852399186;
    }

    public static class Channels
    {
        public static ulong rules = 746786365840293909;
        public static ulong welcome = 720940155485159497;
        public static ulong goodbye = 829326354817876049;
        public static ulong Audit_Log = 785254482144395284;
        public static ulong Alt_Log = 911716037815308378;
        public static ulong Spam_Log = 911715849545596948;
        public static ulong Ban_Log = 859480827682881536;
    }

    public static class Messages
    {
        public static ulong rules_message = 859467047871316018;
    }

    //Information for our URL Sniffer
    public class DomainAge
    {
        public string human { get; set; }
        public int timestamp { get; set; }
        public DateTime iso { get; set; }
    }

    public class Root
    {
        public string message { get; set; }
        public bool success { get; set; }
        public bool @unsafe { get; set; }
        public string domain { get; set; }
        public string server { get; set; }
        public string content_type { get; set; }
        public int status_code { get; set; }
        public int page_size { get; set; }
        public int domain_rank { get; set; }
        public bool dns_valid { get; set; }
        public bool parking { get; set; }
        public bool spamming { get; set; }
        public bool malware { get; set; }
        public bool phishing { get; set; }
        public bool suspicious { get; set; }
        public bool adult { get; set; }
        public int risk_score { get; set; }
        public string category { get; set; }
        public DomainAge domain_age { get; set; }
        public string request_id { get; set; }
    }
}
