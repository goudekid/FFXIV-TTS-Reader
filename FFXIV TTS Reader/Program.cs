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

namespace FFXIV_TTS_Reader
{
    class Program
    {
        const int PROCESS_VM_READ = 0x0010;
        static BackgroundWorker backgroundWorker1 = new BackgroundWorker(); // Create the background worker
        static string input;

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        static async Task Main(string[] args)
        {
            string accessToken;

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

            Process process = Process.GetProcessById(4880);
            IntPtr processHandle = OpenProcess(PROCESS_VM_READ, false, process.Id);

            int bytesRead = 0;
            byte[] buffer = new byte[256]; 
            string newText = "\0";
            string oldText = "\0";
            Console.WriteLine("Enter the address of Dialog Text");
            string stringAddress = Console.ReadLine();
            long address;
            try
            {
                address = Convert.ToInt64(stringAddress, 16);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Bad Address: " + ex.Message);
                return;
            }

            long originalAddress = address;
            bool pause = false;

            while (true)
            {
                if (input == "p" && !pause)
                {
                    pause = true;
                    Console.WriteLine("Paused!");
                    input = "";
                }
                else if (input == "p" && pause)
                {
                    pause = false;
                    Console.WriteLine("Unpaused!");
                    input = "";
                }

                if (!pause)
                {
                    address = originalAddress;

                    ReadProcessMemory((int)processHandle, address++, buffer, buffer.Length, ref bytesRead);

                    List<byte> bufferList = new List<byte>();

                    foreach (byte b in buffer)
                    {
                        if (b == (byte)'\0')
                        {
                            break;
                        }

                        bufferList.Add(b);
                    }

                    newText = Encoding.UTF8.GetString(bufferList.ToArray());

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        buffer[i] = (byte)'\0';
                    }

                    bufferList.Clear();

                    if (oldText != newText)
                    {
                        oldText = newText;
                        string sanitizedText = Regex.Replace(newText, @"[^\u0020-\u007F]+", string.Empty);
                        Console.WriteLine(sanitizedText);

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

                                using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(true))
                                {
                                    response.EnsureSuccessStatusCode();

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
                input = Console.In.ReadLine().Replace("\n", string.Empty);
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
