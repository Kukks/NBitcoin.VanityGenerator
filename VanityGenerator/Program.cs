using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NBitcoin;

namespace VanityGenerator
{
    class Program
    {
        private static readonly Channel<(string, string)> Found = Channel.CreateUnbounded<(string, string)>();


        static async Task Main(string[] args)
        {
            var toMatch = new List<string>();
            while (true)
            {
                Console.Write($"Enter what to match for{(toMatch.Any()? " (or leave blank to start)": string.Empty)}:");
                var entry = Console.ReadLine();
                
                
                if (string.IsNullOrEmpty(entry) && toMatch.Any())
                {
                    Console.WriteLine($"Going to be searching for {string.Join(',', toMatch)}");
                    break;
                }

                toMatch.Add(entry);

            }
            _ = WriteResults();
            _ = Hunt(GetSubset(toMatch));
        }

        private static IEnumerable<string> GetSubset(IEnumerable<string> matches)
        {
            return matches.SelectMany(s =>
            {
                if (s.Length <= 4)
                {
                    return new[] {s.ToLowerInvariant()};
                }
                else
                {
                    var result = new string[s.Length - 3];
                    for (int x = 4; x <= s.Length; x++)
                    {
                        result[x - 4] = s.Substring(0, x).ToLowerInvariant();
                    }

                    return result;
                }
            });
        }

        private static async Task WriteResults()
        {
            while (await Found.Reader.WaitToReadAsync(CancellationToken.None))
            {
                if (Found.Reader.TryRead(out var req))
                {
                    if (string.IsNullOrEmpty(req.Item1) || string.IsNullOrEmpty(req.Item2))
                    {
                        continue;
                    }

                    await File.WriteAllTextAsync(req.Item1 + ".txt", $"{req.Item1}{Environment.NewLine}{req.Item2}",
                        Encoding.UTF8, CancellationToken.None);
                }
            }
        }

        private static async Task Hunt(IEnumerable<string> toMatch)
        {
            while (true)
            {
                var k = new Key();
                var segwit = k.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString().ToLowerInvariant();
                var segwitp2sh = k.PubKey.GetAddress(ScriptPubKeyType.SegwitP2SH, Network.Main).ToString()
                    .ToLowerInvariant();
                var matchesNative = Matches(toMatch, segwit.Substring(4));
                var matchesP2SH = Matches(toMatch, segwitp2sh.Substring(1));
                if (matchesNative.Any() || matchesP2SH.Any())
                {
                    foreach (var s in matchesNative)
                    {
                        var fileName = $"{s} segwit addr={segwit}";
                        var wif = k.GetWif(Network.Main).ToString();
                        Console.WriteLine($"{fileName} wif={wif}");
                        await Found.Writer.WriteAsync((fileName, wif));
                    }

                    foreach (var s in matchesP2SH)
                    {
                        var fileName = $"{s} p2sh addr={segwitp2sh}";
                        var wif = k.GetWif(Network.Main).ToString();
                        Console.WriteLine($"{fileName} wif={wif}");
                        await Found.Writer.WriteAsync((fileName, wif));
                    }
                }
            }
        }

        private static string[] Matches(IEnumerable<string> vanity, string address)
        {
            return vanity.Where(address.StartsWith).ToArray();
        }
    }
}