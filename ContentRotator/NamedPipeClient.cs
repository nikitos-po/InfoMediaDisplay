using System;
using System.IO;
using System.IO.Pipes;
using CommonLib;

/// <summary>
/// Класс управления проигрывателем через именованные каналы4
/// 
/// </summary>

class NamedPipeClient
{
    private static string _pipeName;
    private readonly FileLogger _logger;
    public NamedPipeClient(string pipeName, FileLogger logger)
    {
        if (string.IsNullOrEmpty(pipeName))
            throw new ArgumentNullException();
        _pipeName = pipeName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                    _logger.Debug($"Sent command to named pipe: {_pipeName} - {commandText}");
                }
            }
        }
        catch (TimeoutException)
        {
            var errMsgText = $"Timeout while connecting to named pipe: {_pipeName}";
            _logger.Error(errMsgText);
            Console.WriteLine(errMsgText);
        }
        catch (IOException e)
        {
            var errMsgText = $"IOException while connecting to named pipe: {_pipeName}. {e.Message}";
            _logger.Error(errMsgText);
            Console.WriteLine(errMsgText);
        }
    }
}

