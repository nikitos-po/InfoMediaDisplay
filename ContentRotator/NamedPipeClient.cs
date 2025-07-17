using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

/// <summary>
/// Класс управления проигрывателем через именованные каналы4
/// 
/// </summary>

class NamedPipeClient
{
    private static string _pipeName;

    public NamedPipeClient(string pipeName)
    {
        if (string.IsNullOrEmpty(pipeName))
            throw new ArgumentNullException();
        _pipeName = pipeName;
    }

    public void SendCommand(string commandText)
    {
        //Для проверки доступностии named pipe нет API. Подключаемся сразу.
        try
        {
            using (var pipeStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out))
            {
                pipeStream.Connect(500); // Время на подключение 0,5 секунд
                using (var writer = new StreamWriter(pipeStream))
                {
                    writer.AutoFlush = true;
                    writer.WriteLine(commandText);
                }
            }
        }
        catch (TimeoutException)
        {
            Console.WriteLine("Timed out connecting to the pipe.");
        }
        catch (IOException e)
        {
            Console.WriteLine("IOException: " + e.Message);
        }
    }
}

