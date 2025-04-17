using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TugboatCaptainsPlayground.Services
{
    public class WindowsRoutesManager
    {


        public HashSet<string> GetActiveRouteDomains()
        {
            var domains = new HashSet<string>();

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C route print",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process != null)
                {
                    process.WaitForExit();
                    string output = process.StandardOutput.ReadToEnd();

                    foreach (string line in output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!line.StartsWith(" ") && line.Length > 20 && !line.Contains("Network"))
                        {
                            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 3)
                            {
                                string destination = parts[0];
                                if (!IPAddress.TryParse(destination, out _))
                                {
                                    domains.Add(destination);
                                }
                            }
                        }
                    }
                }
            }

            return domains;
        }

        public List<string> GetRoutesForDomain(string domain)
        {
            var routes = new List<string>();

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C route print",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process != null)
                {
                    process.WaitForExit();
                    string output = process.StandardOutput.ReadToEnd();

                    foreach (string line in output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.Contains($" {domain} ") && !line.StartsWith(" "))
                        {
                            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1)
                            {
                                routes.Add(parts[1]);
                            }
                        }
                    }
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