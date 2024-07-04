using System.Runtime.InteropServices;
using Tmds.DBus;

namespace SecuritySystem.Utils
{
    public enum NetworkManagerState : uint
    {
        Unknown = 0,
        ASleep = 10,
        Disconnected = 20,
        Disconnecting = 30,
        Connecting = 40,
        ConnectedLocal = 50,
        ConnectedSite = 60,
        ConnectedGlobal = 70
    }
    public enum NetworkManagerConnectivity : uint
    {
        Unknown = 0,
        None = 1,
        Portal = 2,
        Limited = 3,
        Full = 4
    }

    [Dictionary]
    public class NetworkManagerProperties : IEnumerable<KeyValuePair<string, object>>
    {
        public bool NetworkingEnabled;
        public bool WirelessEnabled;
        public ObjectPath[]? ActiveConnections;
        public string? Version;
        public NetworkManagerState State;
        public NetworkManagerConnectivity Connectivity;

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object>(nameof(NetworkingEnabled), NetworkingEnabled);
            yield return new KeyValuePair<string, object>(nameof(WirelessEnabled), WirelessEnabled);
            yield return new KeyValuePair<string, object>(nameof(ActiveConnections), ActiveConnections);
            yield return new KeyValuePair<string, object>(nameof(Version), Version);
            yield return new KeyValuePair<string, object>(nameof(State), State);
            yield return new KeyValuePair<string, object>(nameof(Connectivity), Connectivity);
        }
    }

    [DBusInterface("org.freedesktop.NetworkManager")]
    public interface INetworkManager : IDBusObject
    {
        Task<ObjectPath[]> GetDevicesAsync();
        Task<NetworkManagerProperties> GetAllAsync();

        Task<object> GetAsync(string prop);
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }
    [Dictionary]
    public class WPASupplicantScanPollResult : IEnumerable<KeyValuePair<string, object>>
    {
        public int linkspeed;
        public uint noise;
        public int width;
        public uint frequency;
        public int rssi;
        public int avg_rssi;
        public int center_frq1;
        public int center_frq2;

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object>(nameof(linkspeed), linkspeed);
            yield return new KeyValuePair<string, object>(nameof(noise), noise);
            yield return new KeyValuePair<string, object>(nameof(width), width);
            yield return new KeyValuePair<string, object>(nameof(frequency), frequency);
            yield return new KeyValuePair<string, object>(nameof(rssi), rssi);
            yield return new KeyValuePair<string, object>(nameof(avg_rssi), avg_rssi);
            yield return new KeyValuePair<string, object>(nameof(center_frq1), center_frq1);
            yield return new KeyValuePair<string, object>(nameof(center_frq2), center_frq2);
        }
    }
    [Dictionary]
    public class WPASupplicantProperties : IEnumerable<KeyValuePair<string, object>>
    {
        public string? DebugLevel;
        public bool DebugTimestamp;
        public bool DebugShowKeys;
        public ObjectPath[]? Interfaces;


        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object>(nameof(DebugLevel), DebugLevel);
            yield return new KeyValuePair<string, object>(nameof(DebugTimestamp), DebugTimestamp);
            yield return new KeyValuePair<string, object>(nameof(DebugShowKeys), DebugShowKeys);
            yield return new KeyValuePair<string, object>(nameof(Interfaces), Interfaces);
        }
    }

    [DBusInterface("fi.w1.wpa_supplicant1")]
    public interface IWPASupplicant : IDBusObject
    {
        Task<WPASupplicantProperties> GetAllAsync();
    }
    [DBusInterface("fi.w1.wpa_supplicant1.Interface")]
    public interface IWPASupplicantInterface : IDBusObject
    {
        Task DisconnectAsync();
        Task<WPASupplicantScanPollResult> SignalPollAsync();

        Task<object> GetAsync(string prop);
        Task SetAsync(string prop, object val);
        Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    }

    public class NetworkManager
    {
        private static Connection connection = new Connection(Address.System);
        public static NetworkManagerConnectivity ConnectivityStatus { get; set; }

        private static INetworkManager? NetworkManagerObject;
        public static async void Initialize()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine("NetworkManager: skipped as not running on linux");
                return;
            }
            await connection.ConnectAsync();

            NetworkManagerObject = connection.CreateProxy<INetworkManager>("org.freedesktop.NetworkManager", new ObjectPath("/org/freedesktop/NetworkManager"));

            var properties = await NetworkManagerObject.GetAllAsync();
            ConnectivityStatus = properties.Connectivity;

            Console.WriteLine("NetworkManager: WPA suppliciant...");
            var wpa = connection.CreateProxy<IWPASupplicant>("fi.w1.wpa_supplicant1", new ObjectPath("/fi/w1/wpa_supplicant1"));

            var io = await wpa.GetAllAsync();


            var tinterface = io.Interfaces[1];
            var device = connection.CreateProxy<IWPASupplicantInterface>("fi.w1.wpa_supplicant1", tinterface);
            Console.WriteLine("Interface: " + await device.GetAsync("Ifname"));
            Console.WriteLine("WLAN state: " + await device.GetAsync("State"));
            //  var poll= await device.SignalPollAsync();
            //  foreach (var item in poll)
            // {
            //     Console.WriteLine(item);
            //  }
        }
    }
}
