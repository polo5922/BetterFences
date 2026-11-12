using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BetterFences.Services;

/// <summary>
/// Canal entre instances : une seconde instance lancée avec des arguments (menu contextuel de l'Explorateur)
/// les transmet à l'instance déjà ouverte par un tube nommé, puis se ferme.
/// </summary>
public static class InstanceChannel
{
    public static string PipeName { get; set; } = "BetterFences";

    public static bool Send(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(5000);
            var bytes = Encoding.UTF8.GetBytes(string.Join("\n", args));
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Transmission à l'instance ouverte impossible", ex);
            return false;
        }
    }

    public static void Listen(Action<string[]> handler)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var text = await reader.ReadToEndAsync();
                    var args = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length > 0)
                        _ = Application.Current.Dispatcher.BeginInvoke(() => handler(args));
                }
                catch (Exception ex)
                {
                    Log.Error("Canal entre instances", ex);
                    await Task.Delay(1000);
                }
            }
        });
    }
}
