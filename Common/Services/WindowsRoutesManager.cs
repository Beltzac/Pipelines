using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Common.Services
{
    public class WindowsRoutesManager
    {


        public async Task<HashSet<string>> GetActiveRouteDomainsAsync()
        {
            var domains = new HashSet<string>();
            var routes = await GetActiveRoutesAsync();

            foreach (var route in routes)
            {
                if (!IPAddress.TryParse(route.Destination, out _))
                {
                    domains.Add(route.Destination);
                }
            }

            return domains;
        }

        private static List<(string Destination, string Gateway)> _cachedRoutes;
        private static DateTime _cacheTime;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public async Task<List<(string Destination, string Gateway)>> GetActiveRoutesAsync()
        {
            if (_cachedRoutes != null && DateTime.Now - _cacheTime < CacheDuration)
            {
                return _cachedRoutes;
            }

            var routes = new List<(string Destination, string Gateway)>();

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C route print",
                Verb = "runas",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process != null)
                {
                    string output = "";
                    if (!process.WaitForExit(5000)) // Timeout after 5 seconds
                    {
                        Console.WriteLine("route print timed out");
                        return routes;
                    }
                    output = await process.StandardOutput.ReadToEndAsync();

                    bool startParsing = false;
                    foreach (string line in output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.Trim().StartsWith("=========="))
                        {
                            startParsing = true;
                            continue;
                        }

                        if (startParsing && !string.IsNullOrWhiteSpace(line) && !line.StartsWith(" ") && !line.StartsWith("Network"))
                        {
                            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 4)
                            {
                                string destination = parts[0];
                                string gateway = parts[2];
                                routes.Add((destination, gateway));
                            }
                        }
                    }
                }
            }

            _cachedRoutes = routes;
            _cacheTime = DateTime.Now;
            return routes;
        }

        public async Task<List<string>> GetRoutesForDomainAsync(string domain)
        {
            var routes = new List<string>();
            var allRoutes = await GetActiveRoutesAsync();

            foreach (var route in allRoutes)
            {
                if (route.Destination.Contains(domain))
                {
                    routes.Add(route.Gateway);
                }
            }

            return routes;
        }

        public async Task<bool> RemoveRouteAsync(string route)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C route delete {route}",
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true
            };

            using (var process = Process.Start(psi))
            {
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Route delete command failed with exit code {process.ExitCode}");
                    }

                    return true;
                }

                return false;
            }
        }

        public async Task AddRoute(string ip, int interfaceIndex, string gateway)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C route add {ip} mask 255.255.255.255 {gateway} METRIC 1 IF {interfaceIndex} -p",
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true
            };

            await Task.Run(() =>
            {
                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"Route add command failed with exit code {process.ExitCode}");
                        }
                    }
                }
            });
        }

        public async Task DeleteRoute(string ip)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C route delete {ip}",
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true
            };

            await Task.Run(() =>
            {
                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"Route delete command failed with exit code {process.ExitCode}");
                        }
                    }
                }
            });
        }

        public List<string> GetIPAddresses(NetworkInterface ni)
        {
            var addresses = new List<string>();
            foreach (var ip in ni.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    addresses.Add(ip.Address.ToString());
                }
            }
            return addresses;
        }

        public string GetDefaultGateway(NetworkInterface ni)
        {
            try
            {
                var ipProps = ni.GetIPProperties();
                var gateways = ipProps.GatewayAddresses
                .Where(g => g.Address != null && g.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(g => g.Address.ToString())
                .ToList();
                return gateways.FirstOrDefault() ?? "Not available";
            }
            catch
            {
                return "Not available";
            }
        }

        public int GetInterfaceIndex(string interfaceId)
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.Id == interfaceId)
                {
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return ni.GetIPProperties().GetIPv4Properties().Index;
                        }
                    }
                }
            }
            return -1;
        }
    }
}