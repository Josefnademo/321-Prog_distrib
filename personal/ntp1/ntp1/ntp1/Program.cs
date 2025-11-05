using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;
using NodaTime.TimeZones;


/// exercise: https://github.com/Josefnademo/321-Prog_distrib/tree/main/activites/ntp1
namespace NtpTimeApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Part A: Retrieve current time from NTP server
            string ntpServer = "time.google.com"; // NTP server
            DateTime ntpTime = await GetNtpTimeAsync(ntpServer);
            Console.WriteLine($"NTP Time: {ntpTime}");

            // Part B: Display time in different formats
            Console.WriteLine($"Date format 1: {ntpTime:dddd, dd MMMM yyyy}");
            Console.WriteLine($"Date format 2: {ntpTime:dd.MM.yyyy HH:mm:ss}");
            Console.WriteLine($"Date format 3: {ntpTime:dd.MM.yyyy}");

            // ISO 8601 format
            Console.WriteLine($"ISO 8601 format: {ntpTime:yyyy-MM-ddTHH:mm:ssZ}");

            // Part C: Time difference calculation
            DateTime systemTimeUtc = DateTime.UtcNow;
            TimeSpan timeDiff = systemTimeUtc - ntpTime.ToUniversalTime();
            Console.WriteLine($"Time difference: {timeDiff.TotalSeconds:F2} seconds");

            // Correct local time based on NTP time
            DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(ntpTime.ToUniversalTime(), TimeZoneInfo.Local);
            Console.WriteLine($"Corrected local time: {localTime}");

            // Convert local time back to UTC
            DateTime backToUtc = TimeZoneInfo.ConvertTimeToUtc(localTime);
            Console.WriteLine($"Local time back to UTC: {backToUtc}");

            // Part D: Improve resource management using 'using' block
            using (UdpClient client = new UdpClient())
            {
                await GetNtpTimeFromServer(client, ntpServer);
            }

            // Part E: Display time in multiple time zones (world clock)
            await DisplayWorldClocksAsync(ntpTime);

            // Part G: NodaTime Integration and Drift Measurement
            await RunDriftMonitorAsync();
        }

        public static async Task<DateTime> GetNtpTimeAsync(string ntpServer)
        {
            try
            {
                byte[] timeMessage = new byte[48];
                timeMessage[0] = 0x1B; // LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

                IPEndPoint ntpReference = new IPEndPoint(Dns.GetHostAddresses(ntpServer)[0], 123);
                using (UdpClient client = new UdpClient())
                {
                    client.Connect(ntpReference);
                    client.Send(timeMessage, timeMessage.Length);

                    byte[] ntpData = client.Receive(ref ntpReference);
                    DateTime ntpTime = NtpPacket.ToDateTime(ntpData);
                    return ntpTime;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving NTP time: {ex.Message}");
                return DateTime.MinValue;
            }
        }

        public static async Task GetNtpTimeFromServer(UdpClient client, string ntpServer)
        {
            try
            {
                byte[] timeMessage = new byte[48];
                timeMessage[0] = 0x1B; // LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

                IPEndPoint ntpReference = new IPEndPoint(Dns.GetHostAddresses(ntpServer)[0], 123);
                client.Connect(ntpReference);
                client.Send(timeMessage, timeMessage.Length);

                byte[] ntpData = client.Receive(ref ntpReference);
                DateTime ntpTime = NtpPacket.ToDateTime(ntpData);
                Console.WriteLine($"NTP Time from server: {ntpTime}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with NTP server: {ex.Message}");
            }
        }

        public static async Task DisplayWorldClocksAsync(DateTime ntpTime)
        {
            var timeZones = new[]
            {
                ("UTC", TimeZoneInfo.Utc),
                ("New York", TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")),
                ("London", TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")),
                ("Tokyo", TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time")),
                ("Sydney", TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time"))
            };

            Console.WriteLine("\n=== WORLD CLOCK ===");
            foreach (var (name, tz) in timeZones)
            {
                // Ensure ntpTime has DateTimeKind set to Utc
                DateTime ntpTimeUtc = DateTime.SpecifyKind(ntpTime, DateTimeKind.Utc);

                // Now convert from UTC to the desired time zone
                DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(ntpTimeUtc, tz);

                // Display the time in the specified time zone
                Console.WriteLine($"{name}: {localTime:yyyy-MM-dd HH:mm:ss}");
            }

        }


        public static async Task RunDriftMonitorAsync()
        {
            var analyzer = new ClockDriftAnalyzer();
            var corrector = new PredictiveDriftCorrector(analyzer);

            // Calibration initial
            await corrector.CalibrateAsync(5, Duration.FromSeconds(10));

            // Surveillance continue
            var timer = new Timer(async _ =>
            {
                var measurement = await analyzer.MeasureDriftAsync();
                if (measurement != null)
                {
                    Console.WriteLine($"Drift: {measurement.SystemOffset.Milliseconds:F3} ms");
                    var correctedTime = corrector.GetCorrectedTime();
                    Console.WriteLine($"Corrected Time: {correctedTime}");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            Console.WriteLine("Press any key to stop monitoring...");
            Console.ReadKey();
            timer.Dispose();
        }
    }

    // NTP Packet Helper Class
    public static class NtpPacket
    {
        public static DateTime ToDateTime(byte[] ntpData)
        {
            // NTP timestamp starts 1900, but .NET DateTime starts 0001
            const ulong UnixTimeOffset = 2208988800; // 1900-01-01 to 1970-01-01

            // Extract the integer part (the seconds since 1900)
            ulong intPart = (ulong)((ntpData[43] << 24) | (ntpData[42] << 16) | (ntpData[41] << 8) | ntpData[40]);

            // Extract the fractional part (used to calculate the nanoseconds)
            ulong fracPart = (ulong)((ntpData[47] << 24) | (ntpData[46] << 16) | (ntpData[45] << 8) | ntpData[44]);

            // Log the raw NTP timestamp for debugging purposes
            Console.WriteLine($"Raw NTP Timestamp - intPart: {intPart}, fracPart: {fracPart}");

            // Convert the NTP time to Unix time
            ulong unixTime = intPart - UnixTimeOffset;

            // Convert to seconds and nanoseconds
            var seconds = (long)unixTime;
            var nanoseconds = (long)((fracPart * 1000000000) >> 32); // Convert fractional part to nanoseconds

            // Create a DateTime from the Unix timestamp
            var dateTime = new DateTime(1970, 1, 1).AddSeconds(seconds).AddTicks(nanoseconds / 100);
            Console.WriteLine($"Current System Time: {DateTime.UtcNow}");

            return dateTime.ToLocalTime(); // Return the time in local time
        }


    }


    public class DriftMeasurement
    {
        public Duration SystemOffset { get; set; }  // Difference between system time and network time
        public Duration NetworkLatency { get; set; }
    }



    // Drift analyzer and corrective behavior (NodaTime)
    public class ClockDriftAnalyzer
    {
        private readonly IClock _systemClock = SystemClock.Instance;
        private readonly IClock _networkClock = SystemClock.Instance; // Assuming you want to use SystemClock for both

        public async Task<DriftMeasurement> MeasureDriftAsync()
        {
            var startTime = _systemClock.GetCurrentInstant();
            try
            {
                var ntpTime = _networkClock.GetCurrentInstant();
                var latency = _systemClock.GetCurrentInstant() - startTime;

                // Calculate the drift as the difference between system time and network time (this will be a Duration)
                var systemOffset = ntpTime - _systemClock.GetCurrentInstant();  // Duration type

                return new DriftMeasurement
                {
                    SystemOffset = systemOffset,  // Store the Duration
                    NetworkLatency = latency
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error measuring drift: {ex.Message}");
                return null;
            }
        }
    }

    public class PredictiveDriftCorrector
    {
        private readonly ClockDriftAnalyzer _analyzer;
        private Duration _predictedDrift = Duration.Zero;

        public PredictiveDriftCorrector(ClockDriftAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public Instant GetCorrectedTime()
        {
            var systemTime = SystemClock.Instance.GetCurrentInstant();

            // Correct the system time by subtracting the predicted drift duration
            return systemTime - _predictedDrift;  // `Duration` can be subtracted from `Instant`
        }

        public async Task CalibrateAsync(int measurementCount = 10, Duration interval = default)
        {
            if (interval == default) interval = Duration.FromSeconds(30);

            for (int i = 0; i < measurementCount; i++)
            {
                var measurement = await _analyzer.MeasureDriftAsync();
                if (measurement != null)
                {
                    _predictedDrift = measurement.SystemOffset; // Store drift as a Duration
                }

                await Task.Delay((int)interval.TotalMilliseconds);
            }
        }
    }
}