using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Media;
using System.ComponentModel;
using System.Linq;

namespace FFXIV_TTS_Reader
{
    class Program
    {
        const int PROCESS_VM_READ = 0x0010;
        static BackgroundWorker backgroundWorker1 = new BackgroundWorker(); // Create the background worker
        static long pointerAddress;
        static bool paused = false;

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        static async Task Main(string[] args)
        {
            string accessToken;

            Console.WriteLine("THIS PROGRAM WILL BREAK ON GAME UPDATES, CURRENT DATE WHEN THIS WAS MADE IS 6/15/2019");

            // Add your subscription key here
            // If your resource isn't in WEST US, change the endpoint
            Authentication auth = new Authentication("https://westus2.api.cognitive.microsoft.com/sts/v1.0/issueToken", "2296b0d4c98549a887a75372618ba3b3");
            try
            {
                accessToken = await auth.FetchTokenAsync().ConfigureAwait(false);
                Console.WriteLine("Successfully obtained an access token. \n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to obtain an access token.");
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.Message);
                return;
            }

            //Setup a background worker
            backgroundWorker1.DoWork += BackgroundWorker1_DoWork; // This tells the worker what to do once it starts working
            backgroundWorker1.RunWorkerCompleted += BackgroundWorker1_RunWorkerCompleted;  // This tells the worker what to do once its task is completed

            backgroundWorker1.RunWorkerAsync(); // This starts the background worker

            string host = "https://westus2.tts.speech.microsoft.com/cognitiveservices/v1";

            Process process = new Process();
            try
            {
                process = Process.GetProcesses().First(p => p.ProcessName.Contains("ffxiv"));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("Found XIV process\n");

            string newText = "\0";
            string oldText = "\0";

            long originalAddress = pointerAddress;

            while (true)
            {
                byte[] dialogFlagMemory = ProcessMemoryReader.GetBytesAtAddress(process, process.MainModule.BaseAddress.ToInt64() + 0x01AB0BB0, 512, 0x348);

                if (!paused && dialogFlagMemory[0] == 1)
                {
                    // current as of 6/15/2019

                    byte[] dialogMemory = ProcessMemoryReader.GetBytesAtAddress(process, process.MainModule.BaseAddress.ToInt64() + 0x01A8DE18, 512, 0xA8, 0x1F0, 0x0);

                    List<byte> bufferList = new List<byte>();

                    foreach (byte b in dialogMemory)
                    {
                        if (b == (byte)'\0')
                        {
                            break;
                        }

                        bufferList.Add(b);
                    }

                    newText = Encoding.UTF8.GetString(bufferList.ToArray());

                    for (int i = 0; i < dialogMemory.Length; i++)
                    {
                        dialogMemory[i] = (byte)'\0';
                    }

                    bufferList.Clear();

                    if (oldText != newText)
                    {
                        oldText = newText;
                        string sanitizedText = Regex.Replace(newText.Replace("─", "+"), @"[^\u0020-\u007F]+", string.Empty);
                        sanitizedText = sanitizedText.Replace("<", "*").Replace(">", "*").Replace("+", "...");
                        Console.WriteLine(sanitizedText + "\n");

                        string body = @"<speak version='1.0' xmlns='https://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
                                <voice name='Microsoft Server Speech Text to Speech Voice (en-US, BenjaminRUS)'>
                                <prosody rate = '+50.00%'>"
                                            + sanitizedText +
                                        "</prosody>" +
                                        "</voice>" +
                                        "</speak>";

                        using (var client = new HttpClient())
                        {

                            using (var request = new HttpRequestMessage())
                            {
                                // Set the HTTP method
                                request.Method = HttpMethod.Post;
                                // Construct the URI
                                request.RequestUri = new Uri(host);
                                // Set the content type header
                                request.Content = new StringContent(body, Encoding.UTF8, "application/ssml+xml");
                                // Set additional header, such as Authorization and User-Agent
                                request.Headers.Add("Authorization", "Bearer " + accessToken);
                                request.Headers.Add("Connection", "Keep-Alive");
                                // Update your resource name
                                request.Headers.Add("User-Agent", "FFXIV_TTS");
                                request.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
                                // Create a request

                                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(true);

                                try
                                {
                                    response.EnsureSuccessStatusCode();
                                }
                                catch(HttpRequestException httpEx)
                                {
                                    try
                                    {
                                        Console.WriteLine(httpEx.Message + ", attempting to refresh token");
                                        accessToken = await auth.FetchTokenAsync().ConfigureAwait(false);
                                        Console.WriteLine("Successfully refreshed access token. \n");

                                        request.Headers.Remove("Authorization");
                                        request.Headers.Add("Authorization", "Bearer " + accessToken);

                                        response = await client.SendAsync(request).ConfigureAwait(true);

                                        response.EnsureSuccessStatusCode();

                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Failed to obtain an access token.");
                                        Console.WriteLine(ex.ToString());
                                        Console.WriteLine(ex.Message);
                                        return;
                                    }
                                }

                                // Asynchronously read the response
                                using (var dataStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(true))
                                {
                                    using (var player = new SoundPlayer(dataStream))
                                    {
                                        player.Play();
                                    }
                                }

                                response.Dispose();
                            }
                        }
                        System.Threading.Thread.Sleep(500);
                    }
                }
            }
        }

        // This is what the background worker will do in the background
        private static void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            if (Console.KeyAvailable == false)
            {
                System.Threading.Thread.Sleep(300); // prevent the thread from eating too much CPU time
            }
            else
            {
                string newInput = Console.In.ReadLine().Replace("\n", string.Empty);
                if (newInput.StartsWith("a="))
                {
                    pointerAddress = Convert.ToInt64(newInput.Replace("a=", string.Empty), 16);
                }
                else if (newInput == "p")
                {
                    if (newInput == "p" && !paused)
                    {
                        paused = true;
                        Console.WriteLine("Paused!");
                    }
                    else if (newInput == "p" && paused)
                    {
                        paused = false;
                        Console.WriteLine("Unpaused!");
                    }
                }

            }
        }

        // This is what will happen when the worker completes reading
        // a user input; since its task was completed, it will need to restart
        private static void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!backgroundWorker1.IsBusy)
            {
                backgroundWorker1.RunWorkerAsync(); // restart the worker
            }

        }
    }
}
