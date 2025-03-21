using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace DockerComposeCleanUp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                DockerComposeDown();
                await RemoveDockerImagesAsync(new List<string> { "docker-web-app", "sql-server-backup" }, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error:");
                Console.WriteLine(ex.Message);
            }
            finally
            {
                WaitForExit();
            }
        }

        private static void DockerComposeDown()
        {
            ConsoleWriteStepLine(" STEP 1/2: Running 'docker-compose down'");
            Console.WriteLine("Running 'docker-compose down'...");

            string composeYaml = GetEmbeddedComposeFile("DockerComposeCleanUp.docker-compose.yml");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "docker-compose",
                Arguments = "-p test-project -f - down",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;

                process.OutputDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

                process.Start();

                // Write the YAML into stdin
                using (var writer = process.StandardInput)
                {
                    writer.Write(composeYaml);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                Console.WriteLine($"docker compose exited with code {process.ExitCode}\n\n");
            }
        }

        static string GetEmbeddedComposeFile(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Embedded resource '{resourceName}' not found.");

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        static async Task RemoveDockerImagesAsync(IList<string> imageNames, bool force)
        {
            ConsoleWriteStepLine(" STEP 2/2: Removing Docker images");

            Console.WriteLine($"Removing Docker images: {string.Join(", ", imageNames)} (Force: {force})");

            DockerClient client = new DockerClientConfiguration().CreateClient();

            foreach (string imageName in imageNames)
            {
                try
                {
                    var parameters = new ImageDeleteParameters { Force = force };

                    IList<IDictionary<string, string>> responses = await client.Images.DeleteImageAsync(imageName, parameters);

                    foreach (var response in responses)
                    {
                        if (response.TryGetValue("Deleted", out string deleted) && !string.IsNullOrEmpty(deleted))
                            Console.WriteLine($"Deleted: {deleted}");

                        if (response.TryGetValue("Untagged", out string untagged) && !string.IsNullOrEmpty(untagged))
                            Console.WriteLine($"Untagged: {untagged}");
                    }
                }
                catch (DockerApiException ex)
                {
                    Console.WriteLine($"ERROR: Failed to remove image '{imageName}': {ex.Message}");
                    throw;
                }
            }

            Console.WriteLine("Docker images removed successfully!\n\n");
        }


        private static void ConsoleWriteStepLine(string stepLine)
        {
            Console.WriteLine("====================================");
            Console.WriteLine(stepLine);
            Console.WriteLine("====================================");
        }
        private static void WaitForExit()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Press any key to close this window...");
            Console.ResetColor();
            Console.ReadKey();
            Environment.Exit(0);
        }
    }
}
