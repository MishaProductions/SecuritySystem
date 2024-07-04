using MHSApi.API;
using Newtonsoft.Json;
using SecuritySystemApi;

namespace SecuritySystem.Utils
{
    public class User
    {
        public string Username = "";
        public string PasswordHash = "";
        public int ID = 1;
        public UserPermissions Permissions;
    }
    public class UsersDB
    {
        public List<User> Users = new();
        public List<EventLogEntry> EventLog = new();
        /// <summary>
        /// <Token, Username>
        /// </summary>
        public Dictionary<string, string> Tokens = new();

        public Dictionary<int, Zone> Zones = new();
        /// <summary>
        /// Time, zone number
        /// </summary>
        public Dictionary<DateTime, int> AlarmHistory = new();


        public bool SmtpEnabled = false;
        public string SmtpSendTo = "";
        public string SmtpUsername = "";
        public string SmtpPassword = "";
        public string SmtpHost = "";
        /// <summary>
        /// 0: No notifications
        /// 1: On alarm
        /// 2: Zone open/close
        /// </summary>
        public int NotificationLevel = 0;
        public string AccessCode = "03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4"; //1234

        //This used to be part of the StaticVariables class
        public bool SystemArmed = false;
        public bool SystemAlarmState = false;
        public bool InExitDelay = false;
        public bool InEntryDelay = false;
        public int Timer = 10;
        public bool IsZoneOpenedWhenSystemArmed = false;
        public bool SystemSetUp = false;
        public int AlarmZone = -1;
        public bool UseOrangePiDriver = false;

        public int DbVersion = 1;
        public string WeatherCords = "";
    }
    public class Configuration
    {
        public static UsersDB Instance { get; internal set; }
        static Configuration()
        {
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/users.json"))
            {
                Console.WriteLine("users: warning: configuration not found, writing default config");
                Instance = new UsersDB();

                // user: Aadmin, pass: mishahasgoodsecuritysystem
                Instance.Users.Add(new User() { ID = 100, Username = "System Installer", Permissions = UserPermissions.Admin, PasswordHash = "abc" });
                Save();
            }
            Console.WriteLine("Loaded users");


            var j = JsonConvert.DeserializeObject<UsersDB>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/users.json"));
            if (j != null)
            {
                Instance = j;
                if (Instance.Zones.Count == 0)
                {
                    Instance.Zones = new Dictionary<int, Zone>();
                    for (int i = 0; i < 7; i++)
                    {
                        Instance.Zones.Add(i, new Zone());
                    }
                    Console.WriteLine("WARNING: imported zones from built-in configuration");
                }

                if (Instance.Users.Count == 0)
                {
                    Instance.Users.Add(new User() { ID = 100, Username = "System Installer", Permissions = UserPermissions.Admin, PasswordHash = "abc" });
                    Save();
                    Console.WriteLine("WARNING: Since there are no user accounts, created default user accoount");
                }

                if (j.DbVersion == 1)
                {
                    Console.WriteLine("*DATABASE UPDATE*");

                    j.Tokens.Clear();
                    j.DbVersion = 2;
                    Console.WriteLine("*DATABASE UPDATE OK*");
                }

                Save();
            }
            else
            {
                throw new Exception("Failed to read configuration json file");
            }
        }
        public static void Save()
        {
            if (Instance != null)
            {
                File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "/users.json", JsonConvert.SerializeObject(Instance, Formatting.Indented));
            }
        }

        public static bool CheckIfCodeCorrect(string code)
        {
            return SecurityApiController.Sha256(code).ToLower() == Instance.AccessCode.ToLower();
        }
    }
}
